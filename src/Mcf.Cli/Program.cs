using System.CommandLine;
using System.Text;
using Mcf.Cli;

Console.OutputEncoding = Encoding.UTF8;
ConsoleReporter.WriteHeader();

var root = new RootCommand("msgchain — chain HTTP and CEP requests in a single MCF document.")
{
    new RunCommand(),
};

var exitCode = await root.Parse(args).InvokeAsync().ConfigureAwait(false);

ConsoleReporter.WriteFooter();

return exitCode;
