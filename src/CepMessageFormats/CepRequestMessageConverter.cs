using System.Text;
using Cep;

namespace CepMessageFormats;

/// <summary>
/// Provides methods to parse and serialize CEP request messages.
/// </summary>
public class CepRequestMessageConverter
{
    /// <summary>
    /// Parses CEP request text into a <see cref="CepRequestMessage"/> instance.
    /// Header and argument values have <c>${VAR_NAME}</c> placeholders expanded against
    /// the host process environment variables during parse.
    /// </summary>
    /// <param name="raw">Raw CEP request text.</param>
    /// <returns>The parsed CEP request message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="raw"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the request text format is invalid.</exception>
    public static CepRequestMessage Parse(string raw)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(raw, nameof(raw));

        using var reader = new StringReader(raw);

        // Start-Line
        var startLine = ReadNonEmptyLine(reader);
        if (startLine is null)
        {
            throw new FormatException("Missing start-line. Expected: <verb> <command> <protocol>.");
        }

        var (verb, command, protocol) = ParseStartLine(startLine);
        var message = new CepRequestMessage(verb, command, protocol);

        // Headers
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            // blank line terminates headers section
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (IsCommentLine(line))
            {
                continue;
            }

            ParseHeaderLine(message.Headers, line);
        }

        // Arguments
        while ((line = reader.ReadLine()) is not null)
        {
            // ignore blank lines in argument section
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsCommentLine(line))
            {
                continue;
            }

            message.Arguments.Add(ParseArgumentLine(line));
        }

        return message;
    }
    /// <summary>
    /// Serializes a <see cref="CepRequestMessage"/> to raw CEP text.
    /// </summary>
    /// <param name="message">The request message to serialize.</param>
    /// <returns>Raw CEP request text.</returns>
    public static string ToRaw(CepRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        var builder = new StringBuilder();

        builder.AppendLine($"{message.Verb} {message.Command} {message.Protocol}");
        foreach (var (name, value) in message.Headers)
        {
            builder.AppendLine($"{name}: {value}");
        }
        builder.AppendLine();
        foreach (var argument in message.Arguments)
        {
            switch (argument)
            {
                case CommandArgument.TokenArgument token:
                    builder.AppendLine(token.Value);
                    break;
                case CommandArgument.NamedArgument named:
                    builder.AppendLine($"{named.Name} {named.Value}");
                    break;
                default:
                    builder.AppendLine(argument.ToString());
                    break;
            }
        }

        return builder.ToString();
    }

    static bool IsCommentLine(string line)
    {
        return line.TrimStart().StartsWith('#');
    }
    static string? ReadNonEmptyLine(StringReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line) && IsCommentLine(line) == false)
            {
                return line;
            }
        }
        return null;
    }
    static (string Verb, string Command, string Protocol) ParseStartLine(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid start-line: '{line}'. Expected: <verb> <command> <protocol>.");
        }

        return (parts.ElementAt(0), parts.ElementAt(1), parts.ElementAt(2));
    }
    static void ParseHeaderLine(IDictionary<string, string> headers, string line)
    {
        var index = line.IndexOf(':');
        if (index <= 0)
        {
            throw new FormatException($"Invalid header line: '{line}'. Expected: Name: Value");
        }

        var name = line[..index].Trim();
        var value = line[(index + 1)..].Trim();

        if (name.Length == 0)
        {
            throw new FormatException($"Invalid header line: '{line}'. Header name is empty.");
        }

        // allow empty value, but still store it
        headers[name] = value.ExpandEnvironmentVariables();
    }
    static CommandArgument ParseArgumentLine(string line)
    {
        // Normalize leading/trailing whitespace before token parsing.
        var trimmed = line.Trim();

        // Split on the first whitespace and keep the remaining text as the value.
        // Example: "-i video.mp4" => name="-i", value="video.mp4"
        // Example: "--filter_complex [0:v]scale=1280:-2" => name="--filter_complex", value="[0:v]scale=1280:-2"
        int firstWs = trimmed.IndexOfAny(new[] { ' ', '\t' });
        if (firstWs < 0)
        {
            // Single token argument (e.g., "-y" or "output.mp4")
            return CommandArgument.Token(trimmed.ExpandEnvironmentVariables());
        }

        var name = trimmed[..firstWs].Trim();
        var value = trimmed[(firstWs + 1)..].Trim();

        if (name.Length == 0)
        {
            // Defensive fallback.
            return CommandArgument.Token(trimmed.ExpandEnvironmentVariables());
        }
        if (value.Length == 0)
        {
            // "-y " => treat as standalone token, keep as "-y"
            return CommandArgument.Token(name.ExpandEnvironmentVariables());
        }

        // Named arguments are serialized in "name value" form.
        return CommandArgument.Named(name, value.ExpandEnvironmentVariables());
    }
}
