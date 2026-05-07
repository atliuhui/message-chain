using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace HttpMessageFormats;

public class HttpResponseMessageConverter
{
    public static HttpResponseMessage Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw, nameof(raw));

        using var reader = new StringReader(raw);

        // Status-Line
        var statusLine = ReadNonEmptyLine(reader);
        if (statusLine is null)
        {
            throw new FormatException("Missing start-line. Expected: <protocol> <statuscode> <reason>.");
        }

        var (protocol, statusCode, reasonPhrase) = ParseStatusLine(statusLine);
        var message = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            ReasonPhrase = reasonPhrase,
        };

        // Headers
        string? line;
        string? contentType = null;

        while ((line = reader.ReadLine()) is not null)
        {
            // blank line terminates headers section
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            ParseHeaderLine(message, line, contentType);
        }

        // Body
        var body = reader.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(body))
        {
            message.Content = new StringContent(body, Encoding.UTF8);

            if (contentType != null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        return message;
    }
    public static string ToRaw(HttpResponseMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        var builder = new StringBuilder();
        var protocol = $"HTTP/{message.Version.Major}.{message.Version.Minor}";
        var reasonPhrase = GetReasonPhraseToken(message);

        builder.AppendLine($"{protocol} {(int)message.StatusCode} {reasonPhrase}");
        foreach (var header in message.Headers)
        {
            foreach (var value in header.Value)
            {
                builder.AppendLine($"{header.Key}: {value}");
            }
        }
        if (message.Content is not null)
        {
            foreach (var header in message.Content.Headers)
            {
                foreach (var value in header.Value)
                {
                    builder.AppendLine($"{header.Key}: {value}");
                }
            }
        }
        builder.AppendLine();
        if (message.Content is not null)
        {
            builder.Append(message.Content.ReadAsStringAsync().Result);
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
    static (string Protocol, int StatusCode, string? ReasonPhrase) ParseStatusLine(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid status-line: '{line}'. Expected: <protocol> <statuscode> <reason>.");
        }

        if (int.TryParse(parts.ElementAt(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exitCode))
        {
            return (parts.ElementAt(0), exitCode, parts.ElementAtOrDefault(2));
        }
        else
        {
            throw new FormatException("Invalid response exit code.");
        }
    }
    static void ParseHeaderLine(HttpResponseMessage message, string line, string? contentType)
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

        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            contentType = value;
        }

        message.Headers.TryAddWithoutValidation(name, value);
    }
    static string GetReasonPhraseToken(HttpResponseMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ReasonPhrase) == false)
        {
            return string.Concat(message.ReasonPhrase.Where(item => char.IsWhiteSpace(item) == false));
        }

        return Enum.GetName(message.StatusCode) ?? "Unknown";
    }
}
