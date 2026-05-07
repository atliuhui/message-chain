using System.CommandLine;
using Mcf.Cli;

namespace UnitTest;

[TestClass]
public sealed class RunCommandTest
{
    [TestMethod]
    public async Task Run_Environment()
    {
        var raw = @"
### ping
# @name ping
# @kind http
GET {{ 'BASE_URL' | env }} HTTP/1.1
";

        var root = new RootCommand
        {
            new RunCommand(),
        };

        var exitCode = await root.Parse(
        [
            "run",
            "--raw", raw,
            "-e", "BASE_URL=https://baidu.com",
        ]).InvokeAsync().ConfigureAwait(false);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("https://baidu.com", Environment.GetEnvironmentVariable("BASE_URL"));
    }
    [TestMethod]
    public async Task Run_RawChain()
    {
        var raw = @"
### ping
# @name ping
# @kind http
GET {{ BASE_URL }} HTTP/1.1
";

        var root = new RootCommand
        {
            new RunCommand(),
        };

        var exitCode = await root.Parse(
        [
            "run",
            "--raw", raw,
            "-e", "BASE_URL=https://baidu.com",
        ]).InvokeAsync().ConfigureAwait(false);

        Assert.AreEqual(0, exitCode);
    }
}
