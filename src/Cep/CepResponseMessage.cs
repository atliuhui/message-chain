namespace Cep;

/// <summary>
/// Represents a CEP response message with protocol version, exit code, headers, and payload.
/// </summary>
public sealed class CepResponseMessage
{
    /// <summary>
    /// Protocol token of the response (for example, CEP/0.1).
    /// </summary>
    public string Protocol { get; }
    /// <summary>
    /// Process exit code for the executed command.
    /// </summary>
    public int ExitCode { get; }
    /// <summary>
    /// Short status token summarizing the outcome (for example, OK, Timeout, Canceled, Error).
    /// </summary>
    public string Reason { get; }
    /// <summary>
    /// Header fields, represented as name:value pairs.
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Opaque output produced during execution (payload).
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Initializes a new CEP response message.
    /// </summary>
    /// <param name="protocol">Protocol token (for example, CEP/0.1).</param>
    /// <param name="exitCode">Process exit code for the executed command.</param>
    /// <param name="reason">Short status token summarizing the outcome.</param>
    public CepResponseMessage(string protocol, int exitCode, string reason)
    {
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        ExitCode = exitCode;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }
}
