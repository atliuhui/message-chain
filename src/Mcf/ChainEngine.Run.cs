using System.Globalization;

namespace Mcf;

public partial class ChainEngine
{
    int running_flag;

    public async partial Task RunAsync(
        string chainRaw,
        IReadOnlyDictionary<string, string>? seedVariables,
        IProgress<ChainProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chainRaw);

        if (Interlocked.Exchange(ref running_flag, 1) == 1)
        {
            throw new InvalidOperationException(
                $"{nameof(ChainEngine)} does not support concurrent {nameof(RunAsync)} calls on the same instance. Create a separate {nameof(ChainEngine)} per parallel execution.");
        }

        try
        {
            Scope.Variables.Clear();
            Scope.Records.Clear();
            if (seedVariables is not null)
            {
                foreach (var (name, value) in seedVariables)
                {
                    Scope.Variables[name] = value;
                }
            }

            Chain.Raw = chainRaw;
            ParseChain();

            await ExecuteChainAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CurrentStep = null;
            CurrentRecord = null;
            Interlocked.Exchange(ref running_flag, 0);
        }
    }

    async Task ExecuteChainAsync(IProgress<ChainProgress>? progress, CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var step in Chain.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            CurrentStep = step;
            var record = new StepRecord();
            CurrentRecord = record;

            RenderStepHeader();
            ParseStepHeader(index);
            ValidateUniqueStepName(record.Metadata.Name, record.Metadata.Title);

            progress?.Report(new ChainProgress(StepPhase.Started, index, record));
            try
            {
                await ExecuteStepAsync(step, record, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                progress?.Report(new ChainProgress(StepPhase.Completed, index, record));
            }

            if (record.Status == StepStatus.Failed && !record.Metadata.ContinueOnError)
            {
                break;
            }
        }
    }

    async Task ExecuteStepAsync(StepDefinition step, StepRecord record, CancellationToken cancellationToken)
    {
        Scope.Records[record.Metadata.Name] = record;

        if (!record.Metadata.When)
        {
            record.Status = StepStatus.Skipped;
            return;
        }

        foreach (var (name, value) in record.Variables)
        {
            Scope.Variables[name] = value;
        }

        if (record.Metadata.Kind == StepKind.Empty)
        {
            if (ValidateEmptyStep(step))
            {
                record.Status = StepStatus.Success;
            }
            else
            {
                record.Status = StepStatus.Failed;
                AppendNote(record, "Empty step must not contain a non-blank content body.");
            }
            record.Attempts = 1;
            return;
        }

        await ExecuteRequestStepAsync(record, cancellationToken).ConfigureAwait(false);
    }

    async Task ExecuteRequestStepAsync(StepRecord record, CancellationToken cancellationToken)
    {
        // Stage 1: one-time setup (render content + parse request). Failure
        // here is terminal — no retries are attempted.
        var handler = await PrepareRequestAsync(record, cancellationToken).ConfigureAwait(false);
        if (handler is null)
        {
            return;
        }

        // Stage 2: drive attempts through a retry policy. The policy owns the
        // control structure (loop + delay schedule); ExecuteRequestAsync owns
        // a single attempt's execution.
        try
        {
            var policy = new RetryPolicy(record.Metadata.RetryAttempts, record.Metadata.RetryDelays);
            await policy.ExecuteAsync(
                () => ExecuteRequestAsync(record, handler, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (record.Status == StepStatus.Failed && record.Attempts > 1)
            {
                AppendNote(record, $"(after {record.Attempts} attempts)");
            }
        }
        finally
        {
            handler.Dispose();
        }
    }

    async Task<StepHandler?> PrepareRequestAsync(StepRecord record, CancellationToken cancellationToken)
    {
        try
        {
            RenderStepContent();
            return await ParseRequestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.Status = StepStatus.Failed;
            AppendNote(record, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Executes a single request attempt: invoke (or retry-invoke) and parse
    /// response. Updates <paramref name="record"/>'s attempt counter, status,
    /// and note. Returns <c>true</c> when the attempt succeeded (stop
    /// retrying), <c>false</c> when it failed (allow retry).
    /// </summary>
    async Task<bool> ExecuteRequestAsync(StepRecord record, StepHandler handler, CancellationToken chainToken)
    {
        record.Attempts++;

        using var stepCancellation = CreateStepCancellation(record.Metadata.Timeout, chainToken);
        try
        {
            await InvokeAsync(handler, stepCancellation.Token).ConfigureAwait(false);
            await ParseResponseAsync(handler, stepCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (chainToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (
            record.Metadata.Timeout is { } timeout
            && stepCancellation.IsCancellationRequested
            && !chainToken.IsCancellationRequested)
        {
            record.Status = StepStatus.Failed;
            AppendNote(record, $"Step timed out after {timeout.ToString("c", CultureInfo.InvariantCulture)}.");
        }
        catch (Exception ex)
        {
            record.Status = StepStatus.Failed;
            AppendNote(record, ex.Message);
        }

        return record.Status != StepStatus.Failed;
    }

    static void AppendNote(StepRecord record, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        record.Note = string.IsNullOrEmpty(record.Note)
            ? message
            : record.Note + Environment.NewLine + message;
    }

    void ValidateUniqueStepName(string name, string? title)
    {
        if (!Scope.Records.ContainsKey(name))
        {
            return;
        }

        var titleHint = string.IsNullOrWhiteSpace(title) ? string.Empty : $" (title: '{title}')";
        throw new FormatException($"Duplicate step name '{name}'{titleHint}. Step names must be unique within a chain.");
    }

    static bool ValidateEmptyStep(StepDefinition step)
    {
        if (string.IsNullOrEmpty(step.ContentRaw))
        {
            return true;
        }

        using var reader = new StringReader(step.ContentRaw);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return false;
            }
        }
        return true;
    }
    static CancellationTokenSource CreateStepCancellation(TimeSpan? timeout, CancellationToken chainToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(chainToken);
        if (timeout is { } value && value > TimeSpan.Zero)
        {
            cts.CancelAfter(value);
        }
        return cts;
    }
}
