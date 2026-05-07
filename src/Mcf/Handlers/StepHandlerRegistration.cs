namespace Mcf.Handlers;

/// <summary>
/// Describes how a custom <see cref="StepKind"/> participates in chain
/// execution: how to construct a fresh <see cref="StepHandler"/> for each
/// step of that kind. The handler itself owns the protocol-specific
/// success-code semantics (see <see cref="StepHandler.IsSuccessCode"/>).
/// </summary>
/// <param name="Kind">The kind this registration applies to.</param>
/// <param name="HandlerFactory">Factory that produces a fresh, single-use handler per step.</param>
public sealed record StepHandlerRegistration(
    StepKind Kind,
    Func<StepHandler> HandlerFactory);
