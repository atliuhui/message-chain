namespace Cep;

/// <summary>
/// Represents a CEP request message with start-line, headers, and ordered arguments.
/// </summary>
public sealed class CepRequestMessage
{
    /// <summary>
    /// Request verb in the start-line (for example, EXEC).
    /// </summary>
    public string Verb { get; }
    /// <summary>
    /// Executable command name or path in the start-line.
    /// </summary>
    public string Command { get; }
    /// <summary>
    /// Protocol token in the start-line (for example, CEP/0.1).
    /// </summary>
    public string Protocol { get; }
    /// <summary>
    /// Header fields, represented as name:value pairs.
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Ordered arguments.
    /// </summary>
    public IList<CommandArgument> Arguments { get; } = new List<CommandArgument>();

    /// <summary>
    /// Initializes a new CEP request message.
    /// </summary>
    /// <param name="verb">Request verb in the start-line.</param>
    /// <param name="command">Executable command name or path.</param>
    /// <param name="protocol">Protocol token (for example, CEP/0.1).</param>
    public CepRequestMessage(string verb, string command, string protocol)
    {
        Verb = verb ?? throw new ArgumentNullException(nameof(verb));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
    }
}

/// <summary>
/// Represents an ordered argument in a Cep Message request.
/// </summary>
public abstract record CommandArgument
{
    /// <summary>
    /// Represents a single token argument (e.g. <c>-y</c> or <c>output.mp4</c>).
    /// </summary>
    public sealed record TokenArgument(string Value) : CommandArgument;

    /// <summary>
    /// Represents a named argument (e.g. <c>-i video.mp4</c>).
    /// </summary>
    public sealed record NamedArgument(string Name, string Value) : CommandArgument;

    /// <summary>
    /// Creates a standalone command-line token argument.
    /// </summary>
    /// <param name="value">The token value.</param>
    /// <returns>A <see cref="TokenArgument"/> instance.</returns>
    public static CommandArgument Token(string value) => new TokenArgument(value);

    /// <summary>
    /// Creates a named argument in the form <name> <value>.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="value">The argument value.</param>
    /// <returns>A <see cref="NamedArgument"/> instance.</returns>
    public static CommandArgument Named(string name, string value) => new NamedArgument(name, value);
}
