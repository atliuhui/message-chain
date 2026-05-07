using System.Globalization;

namespace Mcf;

public partial class ChainEngine
{
    partial void ParseStepHeader()
    {
        var step = RequireCurrentStep();
        var record = RequireCurrentRecord();

        PopulateMetadata(record.Metadata, record.MetadataRendered);
        record.Metadata.Title = NullIfBlank(step.Title);
        if (string.IsNullOrWhiteSpace(record.Metadata.Name))
        {
            throw new FormatException("Step metadata must include a non-empty # @name value.");
        }

        PopulateVariables(record.Variables, record.VariablesRendered);
    }

    void PopulateMetadata(StepMetadata target, string source)
    {
        foreach (var line in EnumerateNonEmptyLines(source))
        {
            var match = MetadataLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            ApplyMetadata(
                target,
                match.Groups["name"].Value.ToLowerInvariant(),
                match.Groups["value"].Value);
        }
    }
    void ApplyMetadata(StepMetadata target, string name, string value)
    {
        switch (name)
        {
            case "name":
                target.Name = ParseStepName(value);
                break;
            case "kind":
                target.Kind = ParseStepKind(value);
                break;
            case "when":
                target.When = ParseTruthy(value);
                break;
            case "timeout":
                target.Timeout = ParseTimeout(value);
                break;
            case "retry-attempts":
                target.RetryAttempts = ParseRetry(value);
                break;
            case "retry-delays":
                target.RetryDelays = ParseRetryDelay(value);
                break;
            case "expect-codes":
                target.ExpectCodes = ParseExpectCode(value);
                break;
            case "continue-on-error":
                target.ContinueOnError = ParseTruthy(value);
                break;
            case "description":
                target.Description = NullIfBlank(value.Trim());
                break;
        }
    }
    static void PopulateVariables(IDictionary<string, string> target, string source)
    {
        foreach (var line in EnumerateNonEmptyLines(source))
        {
            var match = VariableLineRegex().Match(line);
            if (!match.Success)
            {
                throw new FormatException($"Invalid variable line: '{line}'. Expected '@name = value'.");
            }

            target[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }
    }
    static IEnumerable<string> EnumerateNonEmptyLines(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            yield break;
        }

        using var reader = new StringReader(source);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }
    static string? NullIfBlank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
    static string ParseStepName(string raw)
    {
        var name = raw.Trim();
        if (!VariableNameRegex().IsMatch(name))
        {
            throw new FormatException($"Invalid step name '{raw}'. Expected pattern ^[A-Za-z_][A-Za-z0-9_]*$.");
        }
        return name;
    }
    StepKind ParseStepKind(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return StepKind.Empty;
        }

        var normalized = value.ToLowerInvariant();
        if (normalized == "empty")
        {
            return StepKind.Empty;
        }
        if (TryGetKind(normalized, out var reg))
        {
            return reg.Kind;
        }

        var registered = string.Join(", ", RegisteredKindNames().Select(n => $"'{n}'"));
        throw new FormatException(
            $"Invalid # @kind value '{raw}'. Expected 'empty' or one of: {registered}.");
    }
    static bool ParseTruthy(string raw)
    {
        var value = raw.Trim();
        return value.ToLowerInvariant() switch
        {
            "true" or "yes" or "on" or "1" => true,
            "false" or "no" or "off" or "0" => false,
            _ => throw new FormatException(
                $"Invalid boolean value '{raw}'. Expected one of: true/yes/on/1, false/no/off/0."),
        };
    }
    static TimeSpan? ParseTimeout(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
        {
            return timeSpan;
        }

        throw new FormatException($"Invalid # @timeout value '{raw}'. Expected a TimeSpan literal like '00:00:30' or '0:5:00'.");
    }
    static int ParseRetry(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return 0;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0)
        {
            throw new FormatException($"Invalid # @retry-attempts value '{raw}'. Expected a non-negative integer.");
        }

        return count;
    }
    static IReadOnlyList<TimeSpan> ParseRetryDelay(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return Array.Empty<TimeSpan>();
        }

        var parts = value.Split(',');
        var result = new TimeSpan[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var entry = parts[i].Trim();
            if (entry.Length == 0)
            {
                throw new FormatException(
                    $"Invalid # @retry-delays value '{raw}'. Empty entry at position {i + 1}.");
            }
            if (!TimeSpan.TryParse(entry, CultureInfo.InvariantCulture, out var span) || span < TimeSpan.Zero)
            {
                throw new FormatException(
                    $"Invalid # @retry-delays entry '{entry}'. Expected a non-negative TimeSpan literal like '00:00:01'.");
            }
            result[i] = span;
        }
        return result;
    }
    static IReadOnlyList<(int Min, int Max)> ParseExpectCode(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return Array.Empty<(int, int)>();
        }

        var parts = value.Split(',');
        var result = new (int Min, int Max)[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var entry = parts[i].Trim();
            if (entry.Length == 0)
            {
                throw new FormatException(
                    $"Invalid # @expect-codes value '{raw}'. Empty entry at position {i + 1}.");
            }

            // Each entry is a digit-pattern that may contain 'X'/'x' wildcards.
            // A wildcard expands to 0..9 (e.g. 2XX → 200..299, 31X → 310..319).
            // A pure-digit entry expands to a single value (e.g. 322 → 322..322).
            result[i] = ExpandPattern(entry);
        }
        return result;
    }
    static (int Min, int Max) ExpandPattern(string entry)
    {
        Span<char> minChars = stackalloc char[entry.Length];
        Span<char> maxChars = stackalloc char[entry.Length];
        for (var i = 0; i < entry.Length; i++)
        {
            var ch = entry[i];
            if (ch is 'X' or 'x')
            {
                minChars[i] = '0';
                maxChars[i] = '9';
            }
            else if (ch is >= '0' and <= '9')
            {
                minChars[i] = ch;
                maxChars[i] = ch;
            }
            else
            {
                throw new FormatException(
                    $"Invalid # @expect-codes entry '{entry}'. Each entry must consist of digits and optional 'X' wildcards (e.g. '322', '2XX', '31X').");
            }
        }
        var min = int.Parse(minChars, NumberStyles.None, CultureInfo.InvariantCulture);
        var max = int.Parse(maxChars, NumberStyles.None, CultureInfo.InvariantCulture);
        return (min, max);
    }
}
