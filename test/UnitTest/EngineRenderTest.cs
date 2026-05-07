using Mcf;
using Mcf.Handlers;

namespace UnitTest;

[TestClass]
public sealed class EngineRenderTest
{
    static ChainEngine CreateLoginEngine()
    {
        var engine = new ChainEngine();
        var record = new StepRecord
        {
            Request = new ExchangeMessage
            {
                Metadata =
                {
                    ["method"] = "POST",
                    ["uri"] = "https://example.com/api/login",
                    ["version"] = "HTTP/1.1",
                },
                Headers = { ["Content-Type"] = "application/json" },
                Content = @"{""username"":""alice""}",
            },
            Response = new ExchangeMessage
            {
                Metadata =
                {
                    ["code"] = "200",
                    ["reason"] = "OK",
                    ["version"] = "HTTP/1.1",
                },
                Headers = { ["Content-Type"] = "application/json" },
                Content = @"{""token"":""abc123"",""user"":{""id"":42,""name"":""alice""},""roles"":[""admin"",""user""]}"
            },
            Status = StepStatus.Pending,
        };
        record.Metadata.Name = "login";
        record.Metadata.Title = "Login Step";
        record.Metadata.Kind = StepKind.Http;
        record.Variables["env"] = "dev";
        engine.Scope.Variables["env"] = "prod";
        engine.Scope.Records["login"] = record;
        return engine;
    }

    [TestMethod]
    public void RenderTemplate_ScopeVariable()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ env }}");

        Assert.AreEqual("prod", result);
    }

    [TestMethod]
    public void RenderTemplate_StepMetadataName()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.metadata.name }}");

        Assert.AreEqual("login", result);
    }

    [TestMethod]
    public void RenderTemplate_StepMetadataTitle()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.Metadata.Title }}");

        Assert.AreEqual("Login Step", result);
    }

    [TestMethod]
    public void RenderTemplate_StepMetadataKind()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.metadata.kind }}");

        Assert.AreEqual("http", result);
    }

    [TestMethod]
    public void RenderTemplate_StepVariable()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.Variables.env }}");

        Assert.AreEqual("dev", result);
    }

    [TestMethod]
    public void RenderTemplate_StepStatus()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.status }}");

        Assert.AreEqual("Pending", result);
    }

    [TestMethod]
    public void RenderTemplate_RequestMetadata()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.Request.Metadata.method }}");

        Assert.AreEqual("POST", result);
    }

    [TestMethod]
    public void RenderTemplate_RequestContent()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.Request.Content }}");

        Assert.AreEqual(@"{""username"":""alice""}", result);
    }

    [TestMethod]
    public void RenderTemplate_RequestContentTypeHeader()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.Request.Headers['Content-Type'] }}");

        Assert.AreEqual("application/json", result);
    }

    [TestMethod]
    public void RenderTemplate_ResponseMetadata()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.response.metadata.code }}");

        Assert.AreEqual("200", result);
    }

    [TestMethod]
    public void RenderTemplate_ResponseContentTypeHeader()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.response.headers['Content-Type'] }}");

        Assert.AreEqual("application/json", result);
    }

    [TestMethod]
    public void RenderTemplate_ResponseJsonPathToken()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.response.content | parse_json_path: '$.token' }}");

        Assert.AreEqual("abc123", result);
    }

    [TestMethod]
    public void RenderTemplate_ResponseJsonPathUserName()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.response.content | parse_json_path: '$.user.name' }}");

        Assert.AreEqual("alice", result);
    }

    [TestMethod]
    public void RenderTemplate_JsonPathWildcard()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate(@"{% assign roles = login.response.content | parse_json_path: '$.roles[*]' %}{{ roles | join: "" and "" }}");

        Assert.AreEqual("admin and user", result);
    }

    [TestMethod]
    public void RenderTemplate_ParseJson()
    {
        var engine = CreateLoginEngine();

        var result = engine.RenderTemplate("{{ login.response.content | parse_json | json }}");

        Assert.AreEqual(@"{""token"":""abc123"",""user"":{""id"":42,""name"":""alice""},""roles"":[""admin"",""user""]}", result);
    }

    [TestMethod]
    public void RenderTemplate_CustomFilter()
    {
        var engine = new ChainEngine();
        engine.RegisterFluidFilter("shout", (input, _, _) =>
            new ValueTask<Fluid.Values.FluidValue>(
                new Fluid.Values.StringValue(input.ToStringValue().ToUpperInvariant() + "!")));

        var result = engine.RenderTemplate("{{ 'hello' | shout }}");

        Assert.AreEqual("HELLO!", result);
    }

    [TestMethod]
    public void RenderTemplate_CustomFilter_PerInstanceIsolation()
    {
        var engineA = new ChainEngine();
        engineA.RegisterFluidFilter("shout", (input, _, _) =>
            new ValueTask<Fluid.Values.FluidValue>(
                new Fluid.Values.StringValue(input.ToStringValue().ToUpperInvariant())));

        var engineB = new ChainEngine();

        Assert.AreEqual("HELLO", engineA.RenderTemplate("{{ 'hello' | shout }}"));
        // engineB has no 'shout' filter; Fluid leaves the value untouched.
        Assert.AreEqual("hello", engineB.RenderTemplate("{{ 'hello' | shout }}"));
    }
}
