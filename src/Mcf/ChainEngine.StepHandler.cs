namespace Mcf;

public partial class ChainEngine
{
    readonly Dictionary<string, StepHandlerRegistration> kinds = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a handler factory for a custom step kind. Re-registering an
    /// existing kind overwrites the previous registration.
    /// <see cref="StepKind.Empty"/> cannot be registered. The handler's
    /// <see cref="StepHandler.IsSuccessCode"/> determines the default
    /// success-code semantics when <c># @expect-code</c> is omitted.
    /// </summary>
    public void RegisterStepHandler(StepKind kind, Func<StepHandler> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);
        if (string.IsNullOrEmpty(kind.Name) || kind == StepKind.Empty)
        {
            throw new ArgumentException("Cannot register a handler for the empty step kind.", nameof(kind));
        }
        kinds[kind.Name] = new StepHandlerRegistration(kind, handlerFactory);
    }

    bool TryGetKind(string? name, out StepHandlerRegistration registration)
    {
        if (!string.IsNullOrEmpty(name) && kinds.TryGetValue(name, out var reg))
        {
            registration = reg;
            return true;
        }
        registration = null!;
        return false;
    }

    internal IEnumerable<string> RegisteredKindNames() => kinds.Keys;

    StepHandler CreateHandler(StepKind kind)
    {
        if (kind == StepKind.Empty)
        {
            throw new InvalidOperationException("Empty steps do not have a handler.");
        }
        if (TryGetKind(kind.Name, out var reg))
        {
            return reg.HandlerFactory();
        }
        throw new InvalidOperationException($"Unsupported step kind '{kind}'.");
    }
}
