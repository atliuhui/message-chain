namespace Mcf.Models;

public sealed class ChainDefinition
{
    public string Raw { get; set; } = string.Empty;
    public List<StepDefinition> Steps { get; } = new();
}
