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
    [DataRow(StepStatus.Success, "true")]
    [DataRow(StepStatus.Skipped, "true")]
    [DataRow(StepStatus.Failed, "false")]
    [DataRow(StepStatus.Pending, "false")]
    public void RenderTemplate_IsOkStatusFilter(StepStatus status, string expected)
    {
        var engine = CreateLoginEngine();
        engine.Scope.Records["login"].Status = status;

        var result = engine.RenderTemplate("{{ login.status | is_ok_status }}");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RenderTemplate_IsOkStatusFilter_UsedInIf()
    {
        var engine = CreateLoginEngine();
        engine.Scope.Records["login"].Status = StepStatus.Success;

        var result = engine.RenderTemplate("{% assign ok = login.status | is_ok_status %}{% if ok %}yes{% else %}no{% endif %}");

        Assert.AreEqual("yes", result);
    }

    [TestMethod]
    [DataRow(StepStatus.Success, "false")]
    [DataRow(StepStatus.Skipped, "false")]
    [DataRow(StepStatus.Failed, "true")]
    [DataRow(StepStatus.Pending, "true")]
    public void RenderTemplate_NotFilter_OnIsOkStatus(StepStatus status, string expected)
    {
        var engine = CreateLoginEngine();
        engine.Scope.Records["login"].Status = status;

        var result = engine.RenderTemplate("{{ login.status | is_ok_status | not }}");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RenderTemplate_NotFilter_UsedInIf()
    {
        var engine = CreateLoginEngine();
        engine.Scope.Records["login"].Status = StepStatus.Failed;

        var result = engine.RenderTemplate(
            "{% assign bad = login.status | is_ok_status | not %}{% if bad %}fail{% else %}ok{% endif %}");

        Assert.AreEqual("fail", result);
    }

    [TestMethod]
    // Only nil and the boolean false are falsy in standard Liquid;
    // every other value (including empty string, "false" string, and 0) is truthy.
    [DataRow("false", "true")]
    [DataRow("true", "false")]
    [DataRow("nil", "true")]
    [DataRow("missing", "true")]
    [DataRow("''", "false")]
    [DataRow("'hello'", "false")]
    [DataRow("'false'", "false")]
    [DataRow("0", "false")]
    public void RenderTemplate_NotFilter_Truthiness(string expression, string expected)
    {
        var engine = new ChainEngine();

        var result = engine.RenderTemplate("{{ " + expression + " | not }}");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RenderTemplate_FileExistsFilter_ExistingFile()
    {
        var engine = new ChainEngine();
        var path = Path.GetTempFileName();
        try
        {
            var result = engine.RenderTemplate("{{ '" + path.Replace("\\", "\\\\") + "' | file_exists }}");
            Assert.AreEqual("true", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void RenderTemplate_FileExistsFilter_MissingFile()
    {
        var engine = new ChainEngine();
        var path = Path.Combine(Path.GetTempPath(), "mcf-missing-" + Guid.NewGuid().ToString("N") + ".tmp");

        var result = engine.RenderTemplate("{{ '" + path.Replace("\\", "\\\\") + "' | file_exists }}");

        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void RenderTemplate_FileExistsFilter_Directory()
    {
        var engine = new ChainEngine();
        var dir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        // A directory is not a file -> false.
        var result = engine.RenderTemplate("{{ '" + dir.Replace("\\", "\\\\") + "' | file_exists }}");

        Assert.AreEqual("false", result);
    }

    [TestMethod]
    [DataRow("''")]
    [DataRow("nil")]
    [DataRow("missing")]
    public void RenderTemplate_FileExistsFilter_EmptyOrNil(string expression)
    {
        var engine = new ChainEngine();

        var result = engine.RenderTemplate("{{ " + expression + " | file_exists }}");

        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void RenderTemplate_DirExistsFilter_ExistingDirectory()
    {
        var engine = new ChainEngine();
        var dir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var result = engine.RenderTemplate("{{ '" + dir.Replace("\\", "\\\\") + "' | dir_exists }}");

        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void RenderTemplate_DirExistsFilter_MissingDirectory()
    {
        var engine = new ChainEngine();
        var dir = Path.Combine(Path.GetTempPath(), "mcf-missing-" + Guid.NewGuid().ToString("N"));

        var result = engine.RenderTemplate("{{ '" + dir.Replace("\\", "\\\\") + "' | dir_exists }}");

        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void RenderTemplate_DirExistsFilter_File()
    {
        var engine = new ChainEngine();
        var path = Path.GetTempFileName();
        try
        {
            // A file is not a directory -> false.
            var result = engine.RenderTemplate("{{ '" + path.Replace("\\", "\\\\") + "' | dir_exists }}");
            Assert.AreEqual("false", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow("''")]
    [DataRow("nil")]
    [DataRow("missing")]
    public void RenderTemplate_DirExistsFilter_EmptyOrNil(string expression)
    {
        var engine = new ChainEngine();

        var result = engine.RenderTemplate("{{ " + expression + " | dir_exists }}");

        Assert.AreEqual("false", result);
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
