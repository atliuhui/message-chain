using System.Diagnostics;
using System.Reflection;
using Spectre.Console;

namespace Mcf.Cli;

/// <summary>
/// Receives <see cref="ChainProgress"/> notifications from <see cref="ChainEngine"/>,
/// measures per-step wall-clock duration, and renders a listr2-style task list:
/// the active step shows a spinner, completed steps stay as a single line with
/// a status marker, the step name in a fixed-width column, and a compact suffix.
/// </summary>
sealed class ConsoleReporter : IProgress<ChainProgress>, IDisposable
{
    const int LEFT_COLUMN_WIDTH = 40;

    readonly Dictionary<string, long> start_timestamps = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, TimeSpan> durations = new(StringComparer.OrdinalIgnoreCase);

    TaskCompletionSource? current_completion;
    Task? current_status_task;

    /// <summary>
    /// Per-step durations keyed by <see cref="StepMetadata.Name"/>, populated
    /// as <see cref="StepPhase.Completed"/> notifications arrive.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> Durations => durations;

    /// <summary>
    /// Prints a one-line banner with the tool name and version above the
    /// progress output. Safe to call once during process startup.
    /// </summary>
    public static void WriteHeader()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ConsoleReporter).Assembly;
        var name = "message-chain";
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
        // Trim Source Link build metadata (e.g. "1.2.3+abc1234") for a tidy banner.
        var plus = version.IndexOf('+');
        if (plus >= 0)
        {
            version = version.Substring(0, plus);
        }
        AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(name)}[/] [grey]v{Markup.Escape(version)}[/]");
        var started = DateTime.Now.ToString("F");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(started)}[/]");
    }
    public void Report(ChainProgress value)
    {
        var record = value.Record;
        var name = record.Metadata.Name;

        switch (value.Phase)
        {
            case StepPhase.Started:
                start_timestamps[name] = Stopwatch.GetTimestamp();
                StartSpinner(record);
                break;

            case StepPhase.Completed:
                var duration = start_timestamps.TryGetValue(name, out var start)
                    ? Stopwatch.GetElapsedTime(start)
                    : TimeSpan.Zero;
                durations[name] = duration;
                StopSpinner();
                WriteCompleted(record, duration);
                break;
        }
    }
    public void Dispose() => StopSpinner();
    /// <summary>
    /// Prints a trailing blank line after the run so the prompt is visually
    /// separated from the progress block.
    /// </summary>
    public static void WriteFooter() => AnsiConsole.WriteLine();

    void StartSpinner(StepRecord record)
    {
        var label = $"[bold]{Markup.Escape(record.Metadata.Name)}[/] [grey]({record.Metadata.Kind})[/]";
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        current_completion = tcs;
        current_status_task = Task.Run(() =>
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .Start(label, _ => tcs.Task.GetAwaiter().GetResult()));
    }
    void StopSpinner()
    {
        current_completion?.TrySetResult();
        try
        {
            current_status_task?.Wait();
        }
        catch
        {
            // Reporting must never break the run; swallow status worker faults.
        }
        current_completion = null;
        current_status_task = null;
    }

    static void WriteCompleted(StepRecord record, TimeSpan duration)
    {
        var name = Markup.Escape(record.Metadata.Name);
        var kind = record.Metadata.Kind.ToString();
        var dur = Markup.Escape(RunReport.FormatDuration(duration));
        var attempts = record.Attempts > 1
            ? $" [grey]({record.Attempts})[/]"
            : string.Empty;

        // Left column: "{name} ({kind})" padded to LEFT_COLUMN_WIDTH so durations align.
        var leftPlain = $"{record.Metadata.Name} ({kind})";
        var leftPad = leftPlain.Length < LEFT_COLUMN_WIDTH
            ? new string(' ', LEFT_COLUMN_WIDTH - leftPlain.Length)
            : " ";
        var left = $"{name} [grey]({kind})[/]{leftPad}";

        switch (record.Status)
        {
            case StepStatus.Success:
                AnsiConsole.MarkupLine($"[green]✔[/] {left}[grey]{dur}[/]{attempts}");
                break;
            case StepStatus.Skipped:
                AnsiConsole.MarkupLine($"[grey]◼ {record.Metadata.Name} ({kind}){leftPad}skipped[/]");
                break;
            case StepStatus.Failed:
                var reason = string.IsNullOrWhiteSpace(record.Note)
                    ? string.Empty
                    : $"  [red]← {Markup.Escape(record.Note!)}[/]";
                AnsiConsole.MarkupLine($"[red]✖[/] {left}[grey]{dur}[/]{attempts}{reason}");
                break;
            default:
                AnsiConsole.MarkupLine($"[yellow]?[/] {left}[grey]{dur}[/]{attempts}");
                break;
        }
    }
}
