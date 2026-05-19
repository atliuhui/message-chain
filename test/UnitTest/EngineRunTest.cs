using System.Text;
using Mcf;
using Mcf.Handlers;

namespace UnitTest;

[TestClass]
public sealed class EngineRunTest
{
    [TestMethod]
    public async Task RunAsync_Normal()
    {
        var engine = new ChainEngine();
        var raw = File.ReadAllText(@"examples/chain-request.mcf", Encoding.UTF8);
        await engine.RunAsync(raw);

        Assert.HasCount(2, engine.Scope.Records);

        foreach (var (name, record) in engine.Scope.Records)
        {
            Console.WriteLine($"-- {nameof(record.RequestRaw)} --");
            Console.WriteLine(record.RequestRaw);
            Console.WriteLine($"-- {nameof(record.ResponseRaw)} --");
            Console.WriteLine(record.ResponseRaw);
        }
    }

    [TestMethod]
    public async Task RunAsync_NullChain()
    {
        var engine = new ChainEngine();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.RunAsync(null!));
    }

    [TestMethod]
    public async Task RunAsync_EmptyChain()
    {
        var engine = new ChainEngine();

        await engine.RunAsync(string.Empty);

        Assert.HasCount(0, engine.Scope.Records);
    }

    [TestMethod]
    public async Task RunAsync_EmptyStepBlankContent()
    {
        var engine = new ChainEngine();

        await engine.RunAsync("### Blank\n# @name s1\n");

        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s1"].Status);
    }

    [TestMethod]
    public async Task RunAsync_EmptyStepNonBlankContent()
    {
        var engine = new ChainEngine();

        await engine.RunAsync("### Bad\n# @name s1\nbody\n");

        var record = engine.Scope.Records["s1"];
        Assert.AreEqual(StepStatus.Failed, record.Status);
        Assert.IsNotNull(record.Note);
    }

    [TestMethod]
    public async Task RunAsync_WhenFalse()
    {
        var engine = new ChainEngine();

        await engine.RunAsync("### Skip\n# @name s1\n# @when false\n");

        Assert.AreEqual(StepStatus.Skipped, engine.Scope.Records["s1"].Status);
    }

    [TestMethod]
    public async Task RunAsync_DuplicateName()
    {
        var engine = new ChainEngine();
        var raw = "### a\n# @name dup\n\n### b\n# @name dup\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(raw));
    }

    [TestMethod]
    public async Task RunAsync_MissingName()
    {
        var engine = new ChainEngine();

        await engine.RunAsync("### no-name\n\n### still-no-name\n");

        Assert.AreEqual(2, engine.Scope.Records.Count);
        Assert.IsTrue(engine.Scope.Records.ContainsKey("s1"));
        Assert.IsTrue(engine.Scope.Records.ContainsKey("s2"));
    }

    [TestMethod]
    public async Task RunAsync_StopOnFailure()
    {
        var engine = new ChainEngine();
        var raw = "### a\n# @name a\nbody\n\n### b\n# @name b\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Failed, engine.Scope.Records["a"].Status);
        Assert.IsFalse(engine.Scope.Records.ContainsKey("b"));
    }

    [TestMethod]
    public async Task RunAsync_ContinueOnError()
    {
        var engine = new ChainEngine();
        var raw = "### a\n# @name a\n# @continue-on-error true\nbody\n\n### b\n# @name b\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Failed, engine.Scope.Records["a"].Status);
        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["b"].Status);
    }

    [TestMethod]
    public async Task RunAsync_StepVariable()
    {
        var engine = new ChainEngine();
        var raw = "### a\n# @name a\n@x = 42\n";

        await engine.RunAsync(raw);

        Assert.AreEqual("42", engine.Scope.Variables["x"]);
    }

    [TestMethod]
    public async Task RunAsync_RunningFlagReset()
    {
        var engine = new ChainEngine();
        var bad = "### a\n# @name dup\n\n### b\n# @name dup\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(bad));

        // The running flag must be reset, otherwise a subsequent call would
        // be misreported as a concurrent invocation.
        await engine.RunAsync("### s\n# @name s\n");
        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_PreCancelled()
    {
        var engine = new ChainEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => engine.RunAsync("### s\n# @name s\n", cts.Token));
    }

    [TestMethod]
    public async Task RunAsync_SequentialReuse()
    {
        var engine = new ChainEngine();
        await engine.RunAsync("### a\n# @name a\n@x = 1\n");
        Assert.HasCount(1, engine.Scope.Records);

        await engine.RunAsync("### b\n# @name b\n");

        Assert.HasCount(1, engine.Scope.Records);
        Assert.IsTrue(engine.Scope.Records.ContainsKey("b"));
        Assert.IsFalse(engine.Scope.Records.ContainsKey("a"));
        Assert.IsFalse(engine.Scope.Variables.ContainsKey("x"));
    }

    [TestMethod]
    public async Task RunAsync_SequentialReuse_WithSeedVariables()
    {
        var engine = new ChainEngine();
        var firstSeed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE_URL"] = "https://a.example",
        };
        var secondSeed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE_URL"] = "https://b.example",
        };

        await engine.RunAsync(
            "### a\n# @name a\n@x = {{ BASE_URL }}\n",
            firstSeed,
            progress: null,
            cancellationToken: default);

        Assert.AreEqual("https://a.example", engine.Scope.Variables["x"]);

        await engine.RunAsync(
            "### b\n# @name b\n@x = {{ BASE_URL }}\n",
            secondSeed,
            progress: null,
            cancellationToken: default);

        Assert.HasCount(1, engine.Scope.Records);
        Assert.IsTrue(engine.Scope.Records.ContainsKey("b"));
        Assert.IsFalse(engine.Scope.Records.ContainsKey("a"));
        Assert.AreEqual("https://b.example", engine.Scope.Variables["x"]);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_CepCustomSuccess()
    {
        var engine = new ChainEngine();
        // `cmd /c exit 1` produces ExitCode=1; with @expect-codes 1 it counts as success.
        var raw =
            "### bad-but-ok\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 1\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 1\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_CepRangeAndList()
    {
        var engine = new ChainEngine();
        // 0 plus 31X (310..319) should match exit code 315.
        var raw =
            "### r\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 0, 31X\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 315\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_CepDefaultStillFailsOnNonZero()
    {
        var engine = new ChainEngine();
        var raw =
            "### d\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @continue-on-error true\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 1\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Failed, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_FailsWhenCodeNotInList()
    {
        var engine = new ChainEngine();
        var raw =
            "### f\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 0\n" +
            "# @continue-on-error true\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 7\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Failed, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_InvalidFormatThrows()
    {
        var engine = new ChainEngine();
        var raw =
            "### x\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes abc\n" +
            "EXEC cmd CEP/0.1\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(raw));
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_WildcardCepSuccess()
    {
        var engine = new ChainEngine();
        // 2XX expands to 200..299, which contains exit code 250.
        var raw =
            "### w\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 2XX\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 250\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_WildcardOutOfRangeFails()
    {
        var engine = new ChainEngine();
        var raw =
            "### w\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 2XX\n" +
            "# @continue-on-error true\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 100\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Failed, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_WildcardInvalidCharThrows()
    {
        var engine = new ChainEngine();
        var raw =
            "### w\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 2YY\n" +
            "EXEC cmd CEP/0.1\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(raw));
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_RangeSyntaxRejected()
    {
        var engine = new ChainEngine();
        // 'min-max' range syntax is no longer supported; only digits and 'X'.
        var raw =
            "### r\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 200-299\n" +
            "EXEC cmd CEP/0.1\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(raw));
    }

    [TestMethod]
    public async Task RunAsync_ExpectCode_WildcardCaseInsensitive()
    {
        var engine = new ChainEngine();
        // Lowercase 'x' must behave the same as uppercase 'X'.
        var raw =
            "### c\n" +
            "# @name s\n" +
            "# @kind cep\n" +
            "# @expect-codes 2xx, 3Xx\n" +
            "EXEC cmd CEP/0.1\n" +
            "\n" +
            "/c\n" +
            "exit 350\n";

        await engine.RunAsync(raw);

        Assert.AreEqual(StepStatus.Success, engine.Scope.Records["s"].Status);
    }

    [TestMethod]
    public async Task RunAsync_CustomKind_RegisteredHandlerInvoked()
    {
        var engine = new ChainEngine();
        var handler = new FakeStepHandler();
        var customKind = StepKind.Of("noop");
        engine.RegisterStepHandler(customKind, () => handler);

        var raw =
            "### c\n" +
            "# @name s\n" +
            "# @kind noop\n" +
            "hello-payload\n";

        await engine.RunAsync(raw);

        var record = engine.Scope.Records["s"];
        Assert.AreEqual(StepStatus.Success, record.Status);
        Assert.AreEqual(customKind, record.Metadata.Kind);
        Assert.AreEqual("noop", record.Metadata.Kind.Name);
        Assert.StartsWith("hello-payload", record.RequestRaw!);
        Assert.AreEqual("ok", record.ResponseRaw);
        Assert.AreEqual(1, handler.InvokeCount);
    }

    [TestMethod]
    public async Task RunAsync_UnknownKind_Fails()
    {
        var engine = new ChainEngine();
        var raw =
            "### c\n" +
            "# @name s\n" +
            "# @kind made-up\n";

        await Assert.ThrowsExactlyAsync<FormatException>(() => engine.RunAsync(raw));
    }

    sealed class FakeStepHandler : Mcf.Handlers.StepHandler
    {
        public int InvokeCount { get; private set; }
        public string? LastRequestRaw { get; private set; }

        public override Task<ExchangeMessage> ParseRequestAsync(string raw, CancellationToken cancellationToken)
        {
            LastRequestRaw = raw;
            var message = new ExchangeMessage { Content = raw };
            return Task.FromResult(message);
        }
        public override Task<(string ResponseRaw, int Code)> InvokeAsync(CancellationToken cancellationToken)
        {
            InvokeCount++;
            return Task.FromResult(("ok", 42));
        }
        public override Task<ExchangeMessage> ParseResponseAsync(string raw, CancellationToken cancellationToken)
        {
            var message = new ExchangeMessage { Content = raw };
            return Task.FromResult(message);
        }
        public override bool IsSuccessCode(int code) => code == 42;
    }
}
