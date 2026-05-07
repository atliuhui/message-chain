namespace Mcf.Models;

/// <summary>
/// Lifecycle phase reported through <see cref="ChainEngine"/>'s progress
/// channel for a single step.
/// </summary>
public enum StepPhase
{
    /// <summary>
    /// Reported after the step's header has been rendered and parsed (so
    /// <see cref="StepRecord.Metadata"/> is populated) and just before the
    /// engine begins request preparation / execution.
    /// </summary>
    Started,
    /// <summary>
    /// Reported once the engine has finished processing the step. The
    /// <see cref="StepRecord"/> carries the final
    /// <see cref="StepRecord.Status"/>, <see cref="StepRecord.Attempts"/>,
    /// and any <see cref="StepRecord.Note"/>.
    /// </summary>
    Completed,
}

/// <summary>
/// Single progress notification emitted by <see cref="ChainEngine"/> while
/// running a chain. Receivers can use it to drive UI feedback, measure per
/// step durations, or log structured events.
/// </summary>
/// <param name="Phase">The lifecycle phase being reported.</param>
/// <param name="Index">1-based index of the step within the chain.</param>
/// <param name="Record">The step record (live reference; mutated by the engine after this notification).</param>
public readonly record struct ChainProgress(StepPhase Phase, int Index, StepRecord Record);
