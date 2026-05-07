namespace Mcf.Handlers;

/// <summary>
/// Identifies a step's protocol kind. Built-in kinds are
/// <see cref="Empty"/>, <see cref="Http"/> and <see cref="Cep"/>; additional
/// kinds can be defined by callers and registered with
/// <see cref="ChainEngine.RegisterStepHandler"/>.
/// <para>
/// Equality is value-based on the lowercase <see cref="Name"/>. Always
/// construct instances via the static factory <see cref="Of"/> (or use a
/// predefined static field) so the name is normalized.
/// </para>
/// </summary>
public readonly record struct StepKind
{
    /// <summary>The well-known empty kind: a variable-only step that does not dispatch a request.</summary>
    public static readonly StepKind Empty = new("empty");
    /// <summary>The well-known HTTP kind.</summary>
    public static readonly StepKind Http = new("http");
    /// <summary>The well-known CEP kind.</summary>
    public static readonly StepKind Cep = new("cep");

    StepKind(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The lowercase identifier used in <c># @kind</c> values. Never null
    /// for instances created via <see cref="Of"/> or the predefined fields.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a <see cref="StepKind"/> from a user-supplied name. The name
    /// is trimmed and lowercased; it must match <c>^[A-Za-z][A-Za-z0-9-]*$</c>.
    /// An empty input is returned as <see cref="Empty"/>.
    /// </summary>
    public static StepKind Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return Empty;
        }
        for (var i = 0; i < normalized.Length; i++)
        {
            var character = normalized[i];
            var isValid = (character >= 'a' && character <= 'z')
                || (i > 0 && ((character >= '0' && character <= '9') || character == '-'));
            if (!isValid)
            {
                throw new ArgumentException(
                    $"Invalid step kind name '{name}'. Expected pattern ^[A-Za-z][A-Za-z0-9-]*$.",
                    nameof(name));
            }
        }
        return new StepKind(normalized);
    }

    public override string ToString() => Name ?? "empty";
}
