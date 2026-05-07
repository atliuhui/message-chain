using System.CommandLine;

namespace Mcf.Cli;

/// <summary>
/// <c>msgchain run</c> — parse, render and execute an MCF document.
/// </summary>
sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Execute an MCF (Message Chain Format) document.")
    {
        var fileOption = new Option<FileInfo?>("--file", "-f")
        {
            Description = "Path to an MCF chain file.",
            HelpName = "path",
        };
        fileOption.AcceptLegalFilePathsOnly();

        var rawOption = new Option<string?>("--raw", "-r")
        {
            Description = "Inline MCF chain text (alternative to --file).",
            HelpName = "text",
        };

        var envOption = new Option<string[]>("--env", "-e")
        {
            Description = "Set a variable as KEY=VALUE. Repeatable; later values override earlier ones.",
            HelpName = "KEY=VALUE",
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.OneOrMore,
            DefaultValueFactory = _ => Array.Empty<string>(),
        };
        envOption.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                var raw = token.Value;
                var eq = raw.IndexOf('=');
                if (eq <= 0)
                {
                    result.AddError($"--env expects KEY=VALUE; got '{raw}'.");
                }
            }
        });

        var envFileOption = new Option<FileInfo[]>("--env-file")
        {
            Description = "Load variables from a dotenv-style file. Repeatable; later files override earlier ones.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            DefaultValueFactory = _ => Array.Empty<FileInfo>(),
        };
        envFileOption.AcceptLegalFilePathsOnly();

        var logOption = new Option<FileInfo?>("--log")
        {
            Description = "Write per-step wire log (raw request/response) to a file.",
            HelpName = "path",
        };
        logOption.AcceptLegalFilePathsOnly();

        var reportOption = new Option<FileInfo?>("--report")
        {
            Description = "Write a run summary table to a file.",
            HelpName = "path",
        };
        reportOption.AcceptLegalFilePathsOnly();

        Add(fileOption);
        Add(rawOption);
        Add(envOption);
        Add(envFileOption);
        Add(logOption);
        Add(reportOption);

        Validators.Add(result =>
        {
            var hasFile = result.GetResult(fileOption) is not null;
            var hasRaw = result.GetResult(rawOption) is not null;
            if (hasFile && hasRaw)
            {
                result.AddError("--file and --raw are mutually exclusive.");
            }
            else if (!hasFile && !hasRaw)
            {
                result.AddError("Either --file or --raw must be provided.");
            }
        });

        SetAction((parse, cancellationToken) => ExecuteAsync(
            parse.GetValue(fileOption),
            parse.GetValue(rawOption),
            parse.GetValue(envOption) ?? Array.Empty<string>(),
            parse.GetValue(envFileOption) ?? Array.Empty<FileInfo>(),
            parse.GetValue(logOption),
            parse.GetValue(reportOption),
            cancellationToken));
    }

    static async Task<int> ExecuteAsync(
        FileInfo? file,
        string? raw,
        string[] envEntries,
        FileInfo[] envFiles,
        FileInfo? logFile,
        FileInfo? reportFile,
        CancellationToken cancellationToken)
    {
        string chainRaw;
        try
        {
            chainRaw = raw ?? await File.ReadAllTextAsync(file!.FullName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Failed to read chain: {ex.Message}").ConfigureAwait(false);
            return 2;
        }

        var seedVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var envFile in envFiles)
            {
                foreach (var pair in EnvParser.ParseFile(envFile.FullName))
                {
                    seedVariables[pair.Key] = pair.Value;
                }
            }
            foreach (var entry in envEntries)
            {
                var pair = EnvParser.ParseEntry(entry);
                seedVariables[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Failed to load environment variables: {ex.Message}").ConfigureAwait(false);
            return 2;
        }

        var engine = new ChainEngine();
        foreach (var (key, value) in seedVariables)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }

        var reporter = new ConsoleReporter();
        try
        {
            await engine.RunAsync(chainRaw, seedVariables, reporter, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Run canceled.").ConfigureAwait(false);
            return 130;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Run failed: {ex.Message}").ConfigureAwait(false);
            await TryWriteOutputsAsync(engine.Scope, reporter.Durations, logFile, reportFile).ConfigureAwait(false);
            return 1;
        }

        await TryWriteOutputsAsync(engine.Scope, reporter.Durations, logFile, reportFile).ConfigureAwait(false);

        foreach (var (_, record) in engine.Scope.Records)
        {
            if (record.Status == StepStatus.Failed)
            {
                return 1;
            }
        }
        return 0;
    }
    static async Task TryWriteOutputsAsync(
        ChainScope scope,
        IReadOnlyDictionary<string, TimeSpan> durations,
        FileInfo? logFile,
        FileInfo? reportFile)
    {
        if (logFile is not null)
        {
            try
            {
                await File.WriteAllTextAsync(logFile.FullName, RunReport.RenderLog(scope, durations)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Console.Error.WriteLineAsync($"Failed to write --log: {ex.Message}").ConfigureAwait(false);
            }
        }
        if (reportFile is not null)
        {
            try
            {
                await File.WriteAllTextAsync(reportFile.FullName, RunReport.RenderReport(scope, durations)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Console.Error.WriteLineAsync($"Failed to write --report: {ex.Message}").ConfigureAwait(false);
            }
        }
    }
}
