namespace Mcf.Handlers;

/// <summary>
/// Strategy that owns the parse → invoke → map pipeline for a specific
/// <see cref="StepKind"/>. Instances are stateful and live for one step.
/// <para>
/// Lifecycle: <see cref="ParseRequestAsync"/> runs once and produces the
/// canonical native request kept on the handler. <see cref="InvokeAsync"/> is
/// called once per attempt — first attempt plus up to <c># @retry</c>
/// retries. Implementations are responsible for any cloning required by
/// single-use protocol objects (e.g. <c>HttpRequestMessage</c>); the native
/// request object never leaks onto <see cref="StepRecord"/>.
/// </para>
/// </summary>
public abstract class StepHandler : IDisposable
{
    /// <summary>
    /// Parses the request wire text once per step and stashes the native
    /// request object internally for subsequent <see cref="InvokeAsync"/>
    /// calls.
    /// </summary>
    public abstract Task<ExchangeMessage> ParseRequestAsync(string raw, CancellationToken cancellationToken);
    /// <summary>
    /// Dispatches the parsed native request and returns the response wire text
    /// plus a protocol-specific numeric response code (HTTP status / CEP exit
    /// code). The engine maps the code to a <see cref="StepStatus"/> using the
    /// step's <c># @expect-code</c> metadata. Called once per attempt;
    /// implementations that wrap single-use protocol objects must clone the
    /// stashed template on retry attempts.
    /// </summary>
    public abstract Task<(string ResponseRaw, int Code)> InvokeAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Parses the response wire text from the most recent invoke call.
    /// </summary>
    public abstract Task<ExchangeMessage> ParseResponseAsync(string raw, CancellationToken cancellationToken);
    /// <summary>
    /// Default predicate applied to the response code returned by
    /// <see cref="InvokeAsync"/> when the step's <c># @expect-code</c> list
    /// is empty. Built-in handlers: HTTP <c>200..299</c>, CEP <c>0</c>.
    /// Custom handlers should override this to encode their protocol's
    /// success semantics; the default treats any code as success.
    /// </summary>
    public virtual bool IsSuccessCode(int code) => true;
    public virtual void Dispose() { }
}
