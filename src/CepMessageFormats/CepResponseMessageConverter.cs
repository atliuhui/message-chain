using System.Globalization;
using System.Text;
using Cep;

namespace CepMessageFormats;

/// <summary>
/// Provides methods to parse and serialize CEP response messages.
/// </summary>
public class CepResponseMessageConverter
{
    /// <summary>
    /// Parses a CEP response text into a <see cref="CepResponseMessage"/> instance.
    /// </summary>
    /// <param name="text">Raw CEP response text.</param>
    /// <returns>The parsed CEP response message.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the response text format is invalid.</exception>
    public static CepResponseMessage Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));

        using var reader = new StringReader(text);

        // Status-Line
        var statusLine = ReadNonEmptyLine(reader);
        if (statusLine is null)
        {
            throw new FormatException("Missing start-line. Expected: <protocol> <exitcode> <reason>.");
        }

        var (protocol, exitcode, reason) = ParseStatusLine(statusLine);
        var message = new CepResponseMessage(protocol, exitcode, reason);

        // Headers
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            // blank line terminates headers section
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            ParseHeaderLine(message.Headers, line);
        }

        // Payload
        var payload = reader.ReadToEnd().Trim();
        if (payload.Length > 0)
        {
            message.Payload = payload;
        }

        return message;
    }
    /// <summary>
    /// Serializes a <see cref="CepResponseMessage"/> to raw CEP text.
    /// </summary>
    /// <param name="message">The response message to serialize.</param>
    /// <returns>Raw CEP response text.</returns>
    public static string ToRaw(CepResponseMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        var builder = new StringBuilder();

        builder.AppendLine($"{message.Protocol} {message.ExitCode} {message.Reason}");
        foreach (var (name, value) in message.Headers)
        {
            builder.AppendLine($"{name}: {value}");
        }
        builder.AppendLine();
        if (string.IsNullOrEmpty(message.Payload) == false)
        {
            builder.AppendLine(message.Payload);
        }

        return builder.ToString();
    }

    static string? ReadNonEmptyLine(StringReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }
        return null;
    }
    static (string Protocol, int ExitCode, string Reason) ParseStatusLine(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid status-line: '{line}'. Expected: <protocol> <exitcode> <reason>.");
        }

        if (int.TryParse(parts.ElementAt(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exitCode))
        {
            return (parts.ElementAt(0), exitCode, parts.ElementAt(2));
        }
        else
        {
            throw new FormatException("Invalid response exit code.");
        }
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
        headers[name] = value;
    }
}
