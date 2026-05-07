using System.Globalization;
using HttpMessageFormats;

namespace Mcf.Handlers;

sealed class HttpStepHandler : StepHandler
{
    readonly HttpClient http_client;
    HttpRequestMessage? request_message;
    bool invoked;

    public HttpStepHandler(HttpClient httpClient)
    {
        http_client = httpClient;
    }

    public override async Task<ExchangeMessage> ParseRequestAsync(string raw, CancellationToken cancellationToken)
    {
        var native = HttpRequestMessageConverter.Parse(raw);
        request_message?.Dispose();
        request_message = native;
        invoked = false;
        // MapRequestAsync also buffers the content so it can be re-read on
        // retry attempts when cloning.
        return await MapRequestAsync(native, cancellationToken).ConfigureAwait(false);
    }
    public override async Task<(string ResponseRaw, int Code)> InvokeAsync(CancellationToken cancellationToken)
    {
        var template = request_message
            ?? throw new InvalidOperationException($"{nameof(InvokeAsync)} requires {nameof(ParseRequestAsync)} to be called first.");

        if (!invoked)
        {
            invoked = true;
            return await SendAsync(template, cancellationToken).ConfigureAwait(false);
        }

        // HttpRequestMessage is single-use (SendAsync marks _sendStatus). Clone
        // a fresh instance from the parsed template; its Content is already
        // buffered in memory by ParseRequestAsync, so we can copy the bytes.
        using var clone = await CloneAsync(template, cancellationToken).ConfigureAwait(false);
        return await SendAsync(clone, cancellationToken).ConfigureAwait(false);
    }
    public override async Task<ExchangeMessage> ParseResponseAsync(string raw, CancellationToken cancellationToken)
    {
        using var native = HttpResponseMessageConverter.Parse(raw);
        return await MapResponseAsync(native, cancellationToken).ConfigureAwait(false);
    }
    public override bool IsSuccessCode(int code) => code is >= 200 and < 300;
    public override void Dispose()
    {
        request_message?.Dispose();
        request_message = null;
    }

    async Task<(string ResponseRaw, int Code)> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await http_client
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        var raw = HttpResponseMessageConverter.ToRaw(response);
        return (raw, (int)response.StatusCode);
    }
    static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            // Content was buffered in ParseRequestAsync, so ReadAsByteArrayAsync
            // here just hands back the in-memory copy.
            var bytes = await source.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (var header in source.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = content;
        }

        return clone;
    }
    static async Task<ExchangeMessage> MapRequestAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var result = new ExchangeMessage
        {
            Metadata =
            {
                ["method"] = source.Method.Method,
                ["uri"] = source.RequestUri?.OriginalString ?? string.Empty,
                ["version"] = $"HTTP/{source.Version.Major}.{source.Version.Minor}",
            },
        };

        CopyHeaders(result.Headers, source.Headers);

        if (source.Content is not null)
        {
            CopyHeaders(result.Headers, source.Content.Headers);
            // Buffer once so the same HttpRequestMessage can still be sent afterwards.
            await source.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
            result.Content = await source.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
    static async Task<ExchangeMessage> MapResponseAsync(HttpResponseMessage source, CancellationToken cancellationToken)
    {
        var result = new ExchangeMessage
        {
            Metadata =
            {
                ["code"] = ((int)source.StatusCode).ToString(CultureInfo.InvariantCulture),
                ["reason"] = source.ReasonPhrase ?? string.Empty,
                ["version"] = $"HTTP/{source.Version.Major}.{source.Version.Minor}",
            },
        };

        CopyHeaders(result.Headers, source.Headers);

        if (source.Content is not null)
        {
            CopyHeaders(result.Headers, source.Content.Headers);
            result.Content = await source.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
    static void CopyHeaders(
        IDictionary<string, string> target,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> source)
    {
        foreach (var header in source)
        {
            target[header.Key] = string.Join(", ", header.Value);
        }
    }
}
