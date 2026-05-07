using System.Text;
using HttpMessageFormats;

namespace UnitTest;

[TestClass]
public sealed class HttpConverterTest
{
    [TestMethod]
    [TestCategory("CI")]
    public async Task Parse_Request()
    {
        var raw = File.ReadAllText(@"examples/example-request.http", Encoding.UTF8);
        var request = HttpRequestMessageConverter.Parse(raw);

        var client = new HttpClient();
        var response = await client.SendAsync(request);

        Assert.IsTrue(response.IsSuccessStatusCode);
    }

    [TestMethod]
    [TestCategory("CI")]
    public async Task Parse_Response()
    {
        var raw = File.ReadAllText(@"examples/example-response.http", Encoding.UTF8);
        var response = HttpResponseMessageConverter.Parse(raw);

        Assert.IsTrue(response.IsSuccessStatusCode);
    }
}
