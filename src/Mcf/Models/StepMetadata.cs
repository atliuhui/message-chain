namespace Mcf.Models;

public sealed class StepMetadata
{
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public StepKind Kind { get; set; } = StepKind.Empty;
    public bool When { get; set; } = true;
    /// <summary>
    /// Per-attempt timeout. When retries occur, the timeout applies to each
    /// invoke/parse-response attempt independently, not to the cumulative
    /// duration. Null means no per-step timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
    /// <summary>
    /// Maximum number of retry attempts (not counting the first attempt) for
    /// <see cref="StepKind.Http"/> / <see cref="StepKind.Cep"/> steps that
    /// produce <see cref="StepStatus.Failed"/>. Defaults to <c>0</c> (no retry).
    /// </summary>
    public int RetryAttempts { get; set; }
    /// <summary>
    /// Wait times before each retry attempt: the k-th retry waits the k-th
    /// entry; if k exceeds the list length, the last entry is repeated; an
    /// empty list means no wait.
    /// </summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } = Array.Empty<TimeSpan>();
    /// <summary>
    /// Response codes that mark this step as <see cref="StepStatus.Success"/>.
    /// Each entry is an inclusive integer range (a single integer is
    /// represented as <c>(value, value)</c>). Empty list means use the
    /// kind-specific default: HTTP <c>200..299</c>, CEP <c>0</c>.
    /// </summary>
    public IReadOnlyList<(int Min, int Max)> ExpectCodes { get; set; } = Array.Empty<(int, int)>();
    public bool ContinueOnError { get; set; }
    public string? Description { get; set; }
}
