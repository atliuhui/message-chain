namespace Mcf.Cli;

/// <summary>
/// Parses environment-style variable inputs for the CLI: <c>KEY=VALUE</c>
/// entries from the command line (<c>--env</c>) and dotenv-style files
/// (<c>--env-file</c>).
/// </summary>
/// <remarks>
/// Dotenv format: one assignment per line, with <c>#</c> line comments,
/// blank lines, optional surrounding double or single quotes around the
/// value, and an optional leading <c>export</c> keyword.
/// </remarks>
static class EnvParser
{
    /// <summary>
    /// Parses a single <c>KEY=VALUE</c> entry as supplied via <c>--env</c>.
    /// Both key and value are trimmed of surrounding whitespace.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the entry has no
    /// <c>=</c> or an empty key.</exception>
    public static KeyValuePair<string, string> ParseEntry(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var eq = entry.IndexOf('=');
        if (eq < 0)
        {
            throw new FormatException("Invalid --env entry: expected KEY=VALUE.");
        }

        var key = entry[..eq].Trim();
        var value = entry[(eq + 1)..].Trim();
        if (key.Length == 0)
        {
            throw new FormatException("Invalid --env entry: empty key.");
        }

        return new KeyValuePair<string, string>(key, value);
    }
    /// <summary>
    /// Reads a dotenv-style file and yields its <c>KEY=VALUE</c> entries in
    /// file order.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> ParseFile(string path)
    {
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                throw new FormatException($"Invalid entry in env file '{path}' at line {lineNumber}.");
            }

            var key = line[..eq].TrimEnd();
            var value = line[(eq + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}
