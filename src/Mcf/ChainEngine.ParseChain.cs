using System.Text;

namespace Mcf;

public partial class ChainEngine
{
    partial void ParseChain()
    {
        Chain.Steps.Clear();

        if (string.IsNullOrEmpty(Chain.Raw))
        {
            return;
        }

        var rawBuilder = new StringBuilder();
        var metadataBuilder = new StringBuilder();
        var variablesBuilder = new StringBuilder();
        var contentBuilder = new StringBuilder();
        var isInContent = false;
        string? openingLine = null;

        using var reader = new StringReader(Chain.Raw);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (IsSeparatorLine(line))
            {
                FlushStep(openingLine, rawBuilder, metadataBuilder, variablesBuilder, contentBuilder);
                rawBuilder.Clear();
                metadataBuilder.Clear();
                variablesBuilder.Clear();
                contentBuilder.Clear();
                isInContent = false;
                openingLine = line;
                continue;
            }

            rawBuilder.AppendLine(line);

            if (isInContent)
            {
                contentBuilder.AppendLine(line);
                continue;
            }

            if (IsMetadataLine(line))
            {
                metadataBuilder.AppendLine(line);
            }
            else if (IsVariableLine(line))
            {
                variablesBuilder.AppendLine(line);
            }
            else if (IsBlankLine(line) || IsCommentLine(line))
            {
            }
            else
            {
                contentBuilder.AppendLine(line);
                isInContent = true;
            }
        }

        FlushStep(openingLine, rawBuilder, metadataBuilder, variablesBuilder, contentBuilder);
    }

    void FlushStep(
        string? openingLine,
        StringBuilder raw,
        StringBuilder metadata,
        StringBuilder variables,
        StringBuilder content)
    {
        if (openingLine is null && metadata.Length == 0 && variables.Length == 0 && content.Length == 0)
        {
            return;
        }

        Chain.Steps.Add(new StepDefinition
        {
            Raw = raw.ToString(),
            Title = ParseSeparatorTitle(openingLine),
            MetadataRaw = metadata.ToString(),
            VariablesRaw = variables.ToString(),
            ContentRaw = content.ToString(),
        });
    }
    static string ParseSeparatorTitle(string? openingLine)
    {
        if (openingLine is null)
        {
            return string.Empty;
        }

        var span = openingLine.AsSpan().TrimStart();
        var cursor = 0;
        while (cursor < span.Length && span[cursor] == '#')
        {
            cursor++;
        }
        return span[cursor..].Trim().ToString();
    }
    static bool IsSeparatorLine(string line)
    {
        var span = line.AsSpan().TrimStart();
        return span.Length >= 3 && span[0] == '#' && span[1] == '#' && span[2] == '#';
    }
    static bool IsBlankLine(string line) => string.IsNullOrWhiteSpace(line);
    static bool IsCommentLine(string line)
    {
        var span = line.AsSpan().TrimStart();
        return span.Length > 0 && span[0] == '#';
    }
    static bool IsVariableLine(string line)
    {
        // Cheap prefilter: a variable line must start with '@' after optional
        // whitespace. Skip the regex for the common "starts with something
        // else" case (e.g. content lines, '#' metadata/comments).
        var span = line.AsSpan().TrimStart();
        if (span.Length == 0 || span[0] != '@')
        {
            return false;
        }
        return VariableLineRegex().IsMatch(line);
    }
    static bool IsMetadataLine(string line)
    {
        // Cheap prefilter: a metadata line must start with '#' followed by
        // '@' after optional whitespace. Avoids regex work on most lines.
        var span = line.AsSpan().TrimStart();
        if (span.Length < 2 || span[0] != '#')
        {
            return false;
        }
        var afterHash = span[1..].TrimStart();
        if (afterHash.Length == 0 || afterHash[0] != '@')
        {
            return false;
        }
        return MetadataLineRegex().IsMatch(line);
    }
}
