namespace Mcf.Models;

/// <summary>
/// Protocol-agnostic representation of a single payload exchanged in a step,
/// either the request or the response side of an HTTP/CEP exchange.
/// </summary>
public sealed class ExchangeMessage
{
    /// <summary>
    /// Protocol-specific metadata (HTTP: method/uri/version; CEP: verb/command/protocol).
    /// Keys are case-insensitive (ordinal).
    /// </summary>
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Message headers. Keys are case-insensitive (ordinal).
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? Content { get; set; }
}
