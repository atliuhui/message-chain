using System.Globalization;
using System.Text;
using Cep;
using CepMessageFormats;

namespace Mcf.Handlers;

sealed class CepStepHandler : StepHandler
{
    readonly CepClient cep_client;
    CepRequestMessage? request_message;

    public CepStepHandler(CepClient cepClient)
    {
        cep_client = cepClient;
    }

    public override Task<ExchangeMessage> ParseRequestAsync(string raw, CancellationToken cancellationToken)
    {
        var native = CepRequestMessageConverter.Parse(raw);
        request_message = native;
        return Task.FromResult(MapRequest(native));
    }
    public override Task<(string ResponseRaw, int Code)> InvokeAsync(CancellationToken cancellationToken)
    {
        var request = request_message
            ?? throw new InvalidOperationException($"{nameof(InvokeAsync)} requires {nameof(ParseRequestAsync)} to be called first.");

        // CepRequestMessage is not consumed by CepClient.RunAsync, so the same
        // instance can be reused across the first attempt and any retries.
        return RunAsync(request, cancellationToken);
    }
    public override Task<ExchangeMessage> ParseResponseAsync(string raw, CancellationToken cancellationToken)
    {
        var native = CepResponseMessageConverter.Parse(raw);
        return Task.FromResult(MapResponse(native));
    }
    public override bool IsSuccessCode(int code) => code == 0;

    async Task<(string ResponseRaw, int Code)> RunAsync(CepRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await cep_client.RunAsync(request, cancellationToken).ConfigureAwait(false);

        var raw = CepResponseMessageConverter.ToRaw(response);
        return (raw, response.ExitCode);
    }
    static ExchangeMessage MapRequest(CepRequestMessage source)
    {
        var result = new ExchangeMessage
        {
            Metadata =
            {
                ["verb"] = source.Verb,
                ["command"] = source.Command,
                ["protocol"] = source.Protocol,
            },
        };

        foreach (var (name, value) in source.Headers)
        {
            result.Headers[name] = value;
        }

        if (source.Arguments.Count > 0)
        {
            var builder = new StringBuilder();
            foreach (var argument in source.Arguments)
            {
                switch (argument)
                {
                    case CommandArgument.TokenArgument token:
                        builder.AppendLine(token.Value);
                        break;
                    case CommandArgument.NamedArgument named:
                        builder.AppendLine($"{named.Name} {named.Value}");
                        break;
                }
            }
            result.Content = builder.ToString();
        }

        return result;
    }
    static ExchangeMessage MapResponse(CepResponseMessage source)
    {
        var result = new ExchangeMessage
        {
            Metadata =
            {
                ["code"] = source.ExitCode.ToString(CultureInfo.InvariantCulture),
                ["reason"] = source.Reason,
                ["protocol"] = source.Protocol,
            },
            Content = source.Payload,
        };

        foreach (var (name, value) in source.Headers)
        {
            result.Headers[name] = value;
        }

        return result;
    }
}
