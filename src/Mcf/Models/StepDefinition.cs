namespace Mcf.Models;

public sealed class StepDefinition
{
    public string Raw { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MetadataRaw { get; set; } = string.Empty;
    public string VariablesRaw { get; set; } = string.Empty;
    public string ContentRaw { get; set; } = string.Empty;
}
