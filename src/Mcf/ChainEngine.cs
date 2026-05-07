using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Cep;

namespace Mcf;

/// <summary>
/// Executes a single chain end-to-end. An engine instance owns mutable state
/// (<see cref="Scope"/>, <see cref="Chain"/>, current step/record) and therefore
/// is <b>not thread-safe</b> and does not support concurrent <see cref="RunAsync"/>
/// calls. To run chains in parallel, create one <see cref="ChainEngine"/> per
/// concurrent execution. Sequential reuse on the same instance is supported;
/// each <see cref="RunAsync"/> call resets <see cref="Scope"/>.
/// </summary>
public partial class ChainEngine
{
    [GeneratedRegex(@"^\s*#\s*@(?<name>[A-Za-z][A-Za-z0-9-]*)\s*(?<value>.*)$")]
    private static partial Regex MetadataLineRegex();
    [GeneratedRegex(@"^\s*@(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)$")]
    private static partial Regex VariableLineRegex();
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex VariableNameRegex();

    static readonly HttpClient default_http_client = new() { Timeout = Timeout.InfiniteTimeSpan };
    static readonly CepClient default_cep_client = new();

    /// <summary>
    /// Creates an engine using shared default <see cref="HttpClient"/> and
    /// <see cref="CepClient"/> singletons. Use the parameterized constructor to
    /// inject custom instances (e.g. for testing or per-host configuration).
    /// </summary>
    public ChainEngine() : this(null, null) { }
    public ChainEngine(HttpClient? httpClient, CepClient? cepClient)
    {
        HttpClient = httpClient ?? default_http_client;
        CepClient = cepClient ?? default_cep_client;

        RegisterStepHandler(StepKind.Http, () => new HttpStepHandler(HttpClient));
        RegisterStepHandler(StepKind.Cep, () => new CepStepHandler(CepClient));
    }

    public HttpClient HttpClient { get; }
    public CepClient CepClient { get; }

    public ChainScope Scope { get; } = new();
    public ChainDefinition Chain { get; } = new();
    StepDefinition? CurrentStep { get; set; }
    StepRecord? CurrentRecord { get; set; }

    public partial Task RunAsync(
        string chainRaw,
        IReadOnlyDictionary<string, string>? seedVariables,
        IProgress<ChainProgress>? progress,
        CancellationToken cancellationToken = default);
    public Task RunAsync(
        string chainRaw,
        IProgress<ChainProgress>? progress,
        CancellationToken cancellationToken = default) =>
        RunAsync(chainRaw, seedVariables: null, progress, cancellationToken);
    public Task RunAsync(string chainRaw, CancellationToken cancellationToken = default) =>
        RunAsync(chainRaw, seedVariables: null, progress: null, cancellationToken);
    partial void ParseChain();
    void RenderStepHeader()
    {
        var step = RequireCurrentStep();
        var record = RequireCurrentRecord();

        var context = BuildTemplateContext();
        record.MetadataRendered = RenderTemplate(step.MetadataRaw, context);
        record.VariablesRendered = RenderTemplate(step.VariablesRaw, context);
    }
    partial void ParseStepHeader();
    void RenderStepContent()
    {
        var step = RequireCurrentStep();
        var record = RequireCurrentRecord();

        var context = BuildTemplateContext();
        record.RequestRaw = RenderTemplate(step.ContentRaw, context).Trim('\r', '\n');
    }
    async Task<StepHandler> ParseRequestAsync(CancellationToken cancellationToken)
    {
        var record = RequireCurrentRecord();

        if (string.IsNullOrEmpty(record.RequestRaw))
        {
            throw new InvalidOperationException($"{nameof(ParseRequestAsync)} requires {nameof(CurrentRecord)}.{nameof(StepRecord.RequestRaw)} to be set.");
        }

        var handler = CreateHandler(record.Metadata.Kind);
        record.Request = await handler.ParseRequestAsync(record.RequestRaw, cancellationToken).ConfigureAwait(false);
        return handler;
    }
    async Task InvokeAsync(StepHandler handler, CancellationToken cancellationToken)
    {
        var record = RequireCurrentRecord();
        var (raw, code) = await handler.InvokeAsync(cancellationToken).ConfigureAwait(false);
        record.ResponseRaw = raw;
        record.Status = IsExpectedCode(code, handler, record.Metadata.ExpectCodes)
            ? StepStatus.Success
            : StepStatus.Failed;
    }
    static bool IsExpectedCode(int code, StepHandler handler, IReadOnlyList<(int Min, int Max)> expect)
    {
        if (expect.Count == 0)
        {
            return handler.IsSuccessCode(code);
        }
        foreach (var (min, max) in expect)
        {
            if (code >= min && code <= max)
            {
                return true;
            }
        }
        return false;
    }
    async Task ParseResponseAsync(StepHandler handler, CancellationToken cancellationToken)
    {
        var record = RequireCurrentRecord();

        if (string.IsNullOrEmpty(record.ResponseRaw))
        {
            throw new InvalidOperationException($"{nameof(ParseResponseAsync)} requires {nameof(CurrentRecord)}.{nameof(StepRecord.ResponseRaw)} to be set.");
        }

        record.Response = await handler.ParseResponseAsync(record.ResponseRaw, cancellationToken).ConfigureAwait(false);
    }
    StepDefinition RequireCurrentStep([CallerMemberName] string operation = "") =>
        CurrentStep ?? throw new InvalidOperationException($"{operation} requires {nameof(CurrentStep)} to be set.");
    StepRecord RequireCurrentRecord([CallerMemberName] string operation = "") =>
        CurrentRecord ?? throw new InvalidOperationException($"{operation} requires {nameof(CurrentRecord)} to be set.");
}
