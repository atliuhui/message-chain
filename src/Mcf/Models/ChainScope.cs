namespace Mcf.Models;

public sealed class ChainScope
{
    /// <summary>
    /// Variables shared across the chain. Names must match
    /// <c>^[A-Za-z_][A-Za-z0-9_]*$</c> and are compared case-insensitively
    /// to align with Liquid template member access.
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Step records keyed by step name (case-insensitive), preserving execution order.
    /// </summary>
    public OrderedDictionary<string, StepRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);
}
