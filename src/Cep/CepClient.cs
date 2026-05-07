using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cep;

/// <summary>
/// Executes CEP requests by spawning local command-line processes and collecting their output.
/// </summary>
public sealed class CepClient
{
    static readonly Regex variable_name_regex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    static readonly TimeSpan kill_flush_grace_period = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Default working directory if request does not specify one.
    /// </summary>
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
    /// <summary>
    /// Default text encoding used for both standard output and standard error.
    /// </summary>
    public Encoding StandardEncoding { get; set; } = Encoding.UTF8;
    /// <summary>
    /// When true, the response payload concatenates stdout and stderr.
    /// When false, payload prefers stdout; falls back to stderr if stdout is empty.
    /// </summary>
    public bool MergeStandardOutputAndStandardError { get; set; } = true;
    /// <summary>
    /// Default timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
    /// <summary>
    /// Optional callback invoked for each line received from standard output.
    /// May be invoked concurrently when multiple runs are in flight; must be thread-safe.
    /// </summary>
    public Action<string?>? PrintStandardOutput { get; set; }
    /// <summary>
    /// Optional callback invoked for each line received from standard error.
    /// May be invoked concurrently when multiple runs are in flight; must be thread-safe.
    /// </summary>
    public Action<string?>? PrintStandardError { get; set; }

    /// <summary>
    /// Runs a CEP request and returns the response. The captured stdout/stderr for
    /// this run is available via <see cref="CepResponseMessage.Payload"/>.
    /// </summary>
    public async Task<CepResponseMessage> RunAsync(
        CepRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var timeout = request.Headers.GetTimeout(this.Timeout);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != TimeSpan.Zero)
        {
            linked.CancelAfter(timeout);
        }

        var info = CreateProcessStartInfo(request);
        var startTime = DateTimeOffset.Now;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var proc = new Process
        {
            StartInfo = info,
            EnableRaisingEvents = true,
        };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            stdout.AppendLine(e.Data);
            PrintStandardOutput?.Invoke(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            stderr.AppendLine(e.Data);
            PrintStandardError?.Invoke(e.Data);
        };

        try
        {
            if (!proc.Start())
            {
                return BuildExecutorErrorResponse(
                    request.Protocol, info, startTime, stdout, stderr,
                    errorType: "StartFailed",
                    errorMessage: "Process could not be started.");
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);

            return BuildCompletedResponse(request.Protocol, info, startTime, proc, stdout, stderr);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            var timedOut = timeout > TimeSpan.Zero && !cancellationToken.IsCancellationRequested;

            await TryKillProcessTreeAsync(proc).ConfigureAwait(false);

            return timedOut
                ? BuildTimeoutResponse(request.Protocol, info, startTime, stdout, stderr)
                : BuildCanceledResponse(request.Protocol, info, startTime, stdout, stderr);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return BuildExecutorErrorResponse(
                request.Protocol, info, startTime, stdout, stderr,
                errorType: ex.GetType().FullName ?? ex.GetType().Name,
                errorMessage: ex.Message);
        }
    }

    string BuildPayload(StringBuilder stdout, StringBuilder stderr)
        => MergeStandardOutputAndStandardError
            ? $"{stdout}{stderr}"
            : stdout.Length == 0 ? stderr.ToString() : stdout.ToString();

    ProcessStartInfo CreateProcessStartInfo(CepRequestMessage request)
    {
        var workdir = request.Headers.GetWorkingDirectory(this.WorkingDirectory);
        var info = new ProcessStartInfo
        {
            FileName = request.Command,
            WorkingDirectory = workdir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = request.Headers.GetEncoding(this.StandardEncoding),
            StandardErrorEncoding = request.Headers.GetEncoding(this.StandardEncoding),
        };

        foreach (var (name, value) in request.Headers)
        {
            if (variable_name_regex.IsMatch(name))
            {
                info.Environment[name] = value;
            }
        }

        foreach (var arg in request.Arguments)
        {
            switch (arg)
            {
                case CommandArgument.NamedArgument named:
                    info.ArgumentList.Add(named.Name);
                    info.ArgumentList.Add(named.Value);
                    break;
                case CommandArgument.TokenArgument token:
                    info.ArgumentList.Add(token.Value);
                    break;
                default:
                    info.ArgumentList.Add(arg.ToString() ?? string.Empty);
                    break;
            }
        }

        return info;
    }
    CepResponseMessage BuildCompletedResponse(
        string protocol, ProcessStartInfo info,
        DateTimeOffset startTime,
        Process proc,
        StringBuilder stdout, StringBuilder stderr)
    {
        var reason = proc.ExitCode == Status.OK ? Status.REASON_OK : Status.REASON_UNKNOWN;
        var response = new CepResponseMessage(protocol, proc.ExitCode, reason)
        {
            Payload = BuildPayload(stdout, stderr),
        };

        response.Headers.TrySetValue("Working-Directory", () => info.WorkingDirectory);
        response.Headers.TrySetValue("Process-Id", () => proc.Id.ToString(CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Start-Time", () => startTime.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Exit-Time", () => DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Total-Time", () => proc.TotalProcessorTime.ToString());
        response.Headers.TrySetValue("User-Time", () => proc.UserProcessorTime.ToString());

        return response;
    }
    CepResponseMessage BuildTimeoutResponse(
        string protocol, ProcessStartInfo info,
        DateTimeOffset startTime,
        StringBuilder stdout, StringBuilder stderr)
    {
        var response = new CepResponseMessage(protocol, exitCode: Status.TIMEOUT, reason: Status.REASON_TIMEOUT)
        {
            Payload = BuildPayload(stdout, stderr),
        };

        response.Headers.TrySetValue("Working-Directory", () => info.WorkingDirectory);
        response.Headers.TrySetValue("Start-Time", () => startTime.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Exit-Time", () => DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));

        return response;
    }
    CepResponseMessage BuildCanceledResponse(
        string protocol, ProcessStartInfo info,
        DateTimeOffset startTime,
        StringBuilder stdout, StringBuilder stderr)
    {
        var response = new CepResponseMessage(protocol, exitCode: Status.CANCELED, reason: Status.REASON_CANCELED)
        {
            Payload = BuildPayload(stdout, stderr),
        };

        response.Headers.TrySetValue("Working-Directory", () => info.WorkingDirectory);
        response.Headers.TrySetValue("Start-Time", () => startTime.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Exit-Time", () => DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));

        return response;
    }
    CepResponseMessage BuildExecutorErrorResponse(
        string protocol, ProcessStartInfo info,
        DateTimeOffset startTime,
        StringBuilder stdout, StringBuilder stderr,
        string errorType, string errorMessage)
    {
        var response = new CepResponseMessage(protocol, exitCode: Status.ERROR, reason: Status.REASON_ERROR)
        {
            Payload = BuildPayload(stdout, stderr),
        };

        response.Headers.TrySetValue("Working-Directory", () => info.WorkingDirectory);
        response.Headers.TrySetValue("Start-Time", () => startTime.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Exit-Time", () => DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        response.Headers.TrySetValue("Error-Type", () => errorType);
        response.Headers.TrySetValue("Error-Message", () => errorMessage);

        return response;
    }
    static async Task TryKillProcessTreeAsync(Process proc)
    {
        try
        {
            if (proc.HasExited)
            {
                return;
            }

            proc.Kill(entireProcessTree: true);

            // Give the OS a brief moment to flush buffered stdout/stderr before the caller reads them.
            using var grace = new CancellationTokenSource(kill_flush_grace_period);
            try
            {
                await proc.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Grace period expired; proceed without further waiting.
            }
        }
        catch
        {
            // Best-effort kill; ignore races and access errors.
        }
    }

    /// <summary>
    /// Conventional CEP exit codes and reason tokens.
    /// </summary>
    static class Status
    {
        public const int OK = 0;
        public const int ERROR = 125;        // executor failed to start/run the process
        public const int TIMEOUT = 124;      // run exceeded the configured timeout
        public const int CANCELED = 130;     // caller-cancelled (SIGINT-like)

        public const string REASON_OK = "OK";
        public const string REASON_UNKNOWN = "Unknown";
        public const string REASON_ERROR = "Error";
        public const string REASON_TIMEOUT = "Timeout";
        public const string REASON_CANCELED = "Canceled";
    }
}
