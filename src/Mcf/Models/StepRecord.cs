namespace Mcf.Models;

public sealed class StepRecord
{
    public string MetadataRendered { get; internal set; } = string.Empty;
    public StepMetadata Metadata { get; } = new();
    public string VariablesRendered { get; internal set; } = string.Empty;
    /// <summary>
    /// Variables declared by this step. Names must match
    /// <c>^[A-Za-z_][A-Za-z0-9_]*$</c> and are compared case-insensitively
    /// to align with Liquid template member access.
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RequestRaw { get; internal set; }
    public ExchangeMessage? Request { get; internal set; }
    public string? ResponseRaw { get; internal set; }
    public ExchangeMessage? Response { get; internal set; }
    public StepStatus Status { get; internal set; }
    /// <summary>
    /// The number of times the invoke / parse-response stages were actually
    /// executed for this step. <c>0</c> for <see cref="StepStatus.Skipped"/>
    /// steps and for <see cref="StepKind.Http"/> / <see cref="StepKind.Cep"/>
    /// steps that failed before the first invoke; <c>1</c> for
    /// <see cref="StepKind.Empty"/> steps; <c>≥ 1</c> for invoked
    /// <see cref="StepKind.Http"/> / <see cref="StepKind.Cep"/> steps (the
    /// first attempt plus any retries triggered by <c># @retry</c>).
    /// </summary>
    public int Attempts { get; internal set; }
    /// <summary>
    /// Free-form note describing the step outcome. On failure, each failed
    /// attempt's diagnostic message is appended (newline-separated). When
    /// retries occurred and the final status is <see cref="StepStatus.Failed"/>,
    /// a trailing <c>" (after N attempts)"</c> summary is appended.
    /// </summary>
    public string? Note { get; internal set; }
}
