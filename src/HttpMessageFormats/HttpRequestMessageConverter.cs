using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace HttpMessageFormats;

public class HttpRequestMessageConverter
{
    public static HttpRequestMessage Parse(string raw)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(raw, nameof(raw));

        using var reader = new StringReader(raw);

        // Start-Line
        var startLine = ReadNonEmptyLine(reader);
        if (startLine is null)
        {
            throw new FormatException("Missing start-line. Expected: <verb> <command> <protocol>.");
        }

        var (method, uri, protocol) = ParseStartLine(startLine);
        var message = new HttpRequestMessage(new HttpMethod(method), uri);

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

            if (IsCommentLine(line))
            {
                continue;
            }

            ParseHeaderLine(message, line, contentType);
        }

        // Body
        var body = reader.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(body))
        {
            message.Content = Factory(body, contentType);
        }

        return message;
    }
    public static string ToRaw(HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));

        if (message.RequestUri is null)
        {
            throw new InvalidOperationException("Cannot serialize an HTTP request without a request URI.");
        }

        var builder = new StringBuilder();
        var requestTarget = GetRequestTarget(message.RequestUri);
        var protocol = $"HTTP/{message.Version.Major}.{message.Version.Minor}";

        builder.AppendLine($"{message.Method.Method} {requestTarget} {protocol}");
        foreach (var header in message.Headers)
        {
            foreach (var value in header.Value)
            {
                builder.AppendLine($"{header.Key}: {value}");
            }
        }
        if (string.IsNullOrWhiteSpace(message.Headers.Host) == false)
        {
            builder.AppendLine($"Host: {message.Headers.Host}");
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
    static (string Method, string Uri, string Protocol) ParseStartLine(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid start-line: '{line}'. Expected: <method> <uri> <protocol>.");
        }

        return (parts.ElementAt(0), parts.ElementAt(1), parts.ElementAt(2));
    }
    static void ParseHeaderLine(HttpRequestMessage message, string line, string? contentType)
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
    static HttpContent Factory(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return new StringContent(body, Encoding.UTF8);
        }

        var type = MediaTypeHeaderValue.Parse(contentType);

        return type.MediaType switch
        {
            "application/json" =>
                new StringContent(body, Encoding.UTF8, "application/json"),

            "application/xml" or "text/xml" =>
                new StringContent(body, Encoding.UTF8, type.MediaType),

            "application/x-www-form-urlencoded" =>
                new FormUrlEncodedContent(ParseForm(body)),

            "multipart/form-data" =>
                throw new NotSupportedException(
                    "multipart/form-data is not supported in raw HTTP message parsing. " +
                    "Use MultipartFormDataContent via API instead."),

            _ =>
                new StringContent(body, Encoding.UTF8, type.MediaType)
        };
    }
    static IEnumerable<KeyValuePair<string, string>> ParseForm(string body)
    {
        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item =>
            {
                var parts = item.Split('=', 2);
                return new KeyValuePair<string, string>(
                    WebUtility.UrlDecode(parts.ElementAt(0)),
                    WebUtility.UrlDecode(parts.ElementAtOrDefault(1) ?? string.Empty));
            });
    }
    static string GetRequestTarget(Uri requestUri)
    {
        if (requestUri.IsAbsoluteUri == false)
        {
            return requestUri.OriginalString;
        }

        var pathAndQuery = requestUri.PathAndQuery;
        return string.IsNullOrEmpty(pathAndQuery) ? "/" : pathAndQuery;
    }
}
