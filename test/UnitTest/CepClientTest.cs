using Cep;

namespace UnitTest;

[TestClass]
public sealed class CepClientTest
{
    [TestMethod]
    [TestCategory("CI")]
    public async Task RunAsync_Normal()
    {
        var request = new CepRequestMessage("EXEC", "dotnet", "CEP/0.1");
        request.Arguments.Add(CommandArgument.Token("--version"));

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(0, response.ExitCode);
    }
    [TestMethod]
    [TestCategory("CI")]
    public async Task RunAsync_Concurrent()
    {
        var client = new CepClient();
        var count = 32;

        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            var request = new CepRequestMessage("EXEC", "dotnet", "CEP/0.1");
            request.Arguments.Add(CommandArgument.Token("--version"));

            var response = await client.RunAsync(request).ConfigureAwait(false);
            return (index, response);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < count; i++)
        {
            var (index, response) = results[i];
            Assert.AreEqual(0, response.ExitCode);

            Console.WriteLine($"#{index} {response.Payload}");
        }
    }

    [TestMethod]
    public async Task RunAsync_Timeout()
    {
        var request = new CepRequestMessage("EXEC", "ping", "CEP/0.1");
        request.Headers["Charset"] = "GBK";
        request.Headers["Timeout"] = "1";            // 1 second
        request.Arguments.Add(CommandArgument.Named("-n", "10"));
        request.Arguments.Add(CommandArgument.Token("127.0.0.1"));

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(124, response.ExitCode);
        Assert.AreEqual("Timeout", response.Reason);

        Console.WriteLine(response.Payload);
    }
    [TestMethod]
    public async Task RunAsync_Canceled()
    {
        var request = new CepRequestMessage("EXEC", "ping", "CEP/0.1");
        request.Headers["Charset"] = "GBK";
        request.Headers["Timeout"] = "30";            // 30 second
        request.Arguments.Add(CommandArgument.Named("-n", "10"));
        request.Arguments.Add(CommandArgument.Token("127.0.0.1"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));     // caller cancels after 1 s

        var client = new CepClient();
        var response = await client.RunAsync(request, cts.Token);

        Assert.AreEqual(130, response.ExitCode);
        Assert.AreEqual("Canceled", response.Reason);

        Console.WriteLine(response.Payload);
    }
    [TestMethod]
    public async Task RunAsync_Error()
    {
        // Spawning a command that doesn't exist makes Process.Start throw a
        // Win32Exception, which CepClient maps to an executor-error response.
        var request = new CepRequestMessage(
            "EXEC",
            $"__cep_no_such_command_{Guid.NewGuid():N}",
            "CEP/0.1");

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(125, response.ExitCode, "executor-error exit code");
        Assert.AreEqual("Error", response.Reason);

        Assert.IsTrue(response.Headers.ContainsKey("Error-Type"), "Error-Type header missing");
        Assert.IsTrue(response.Headers.ContainsKey("Error-Message"), "Error-Message header missing");

        var errorType = response.Headers["Error-Type"];
        var errorMessage = response.Headers["Error-Message"];

        Assert.IsFalse(string.IsNullOrWhiteSpace(errorType), "Error-Type should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage), "Error-Message should not be empty");

        // Process.Start throws Win32Exception when the command cannot be found.
        StringAssert.Contains(errorType, "Win32Exception", $"unexpected Error-Type: {errorType}");

        // Lifecycle headers populated even on failure paths.
        Assert.IsTrue(response.Headers.ContainsKey("Start-Time"));
        Assert.IsTrue(response.Headers.ContainsKey("Exit-Time"));
        Assert.IsTrue(response.Headers.ContainsKey("Working-Directory"));

        // No process ever ran, so there should be no Process-Id header.
        Assert.IsFalse(response.Headers.ContainsKey("Process-Id"));

        Console.WriteLine($"Error-Type: {errorType}");
        Console.WriteLine($"Error-Message: {errorMessage}");
    }
}
