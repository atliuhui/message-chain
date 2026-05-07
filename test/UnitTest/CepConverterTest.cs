using System.Text;
using Cep;
using CepMessageFormats;

namespace UnitTest;

[TestClass]
public sealed class CepConverterTest
{
    [TestMethod]
    [TestCategory("CI")]
    public async Task Parse_Normal()
    {
        var raw = File.ReadAllText(@"examples/dotnet-request.cep", Encoding.UTF8);
        var request = CepRequestMessageConverter.Parse(raw);

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(0, response.ExitCode);
    }
    [TestMethod]
    [TestCategory("CI")]
    public async Task Parse_Response()
    {
        var raw = File.ReadAllText(@"examples/dotnet-response.cep", Encoding.UTF8);
        var response = CepResponseMessageConverter.Parse(raw);

        Assert.AreEqual(0, response.ExitCode);
    }

    [TestMethod]
    public async Task Parse_echo()
    {
        var raw = File.ReadAllText(@"examples/echo-request.cep", Encoding.UTF8);
        var request = CepRequestMessageConverter.Parse(raw);

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(0, response.ExitCode);

        Console.WriteLine(CepResponseMessageConverter.ToRaw(response));
    }
    [TestMethod]
    public async Task Parse_ffmpeg()
    {
        var raw = File.ReadAllText(@"examples/ffmpeg-request.cep", Encoding.UTF8);
        var request = CepRequestMessageConverter.Parse(raw);

        var client = new CepClient();
        var response = await client.RunAsync(request);

        Assert.AreEqual(0, response.ExitCode);

        Console.WriteLine(CepResponseMessageConverter.ToRaw(response));
    }
}
