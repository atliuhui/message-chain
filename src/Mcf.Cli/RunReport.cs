using System.Globalization;
using System.Text;

namespace Mcf.Cli;

/// <summary>
/// Renders a <see cref="ChainScope"/> after a run into two text artefacts:
/// a verbose wire log (raw request/response pairs) and a compact summary
/// report (status, attempts, duration, notes).
/// </summary>
static class RunReport
{
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }
        else if (duration.TotalSeconds >= 1)
        {
            return duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s";
        }
        else
        {
            return duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + "ms";
        }
    }
    public static string RenderLog(ChainScope scope, IReadOnlyDictionary<string, TimeSpan> durations)
    {
        var builder = new StringBuilder();
        var index = 0;
        foreach (var (name, record) in scope.Records)
        {
            index++;
            builder.Append("### Step ").Append(index).Append(": ").Append(name);
            if (!string.IsNullOrWhiteSpace(record.Metadata.Title))
            {
                builder.Append(" — ").Append(record.Metadata.Title);
            }
            builder.AppendLine();
            AppendField(builder, "Kind", record.Metadata.Kind.ToString());
            AppendField(builder, "Status", record.Status.ToString());
            AppendField(builder, "Attempts", record.Attempts.ToString(CultureInfo.InvariantCulture));
            if (durations.TryGetValue(name, out var duration))
            {
                AppendField(builder, "Duration", FormatDuration(duration));
            }
            if (!string.IsNullOrEmpty(record.Note))
            {
                AppendField(builder, "Note", record.Note);
            }
            builder.AppendLine();

            if (!string.IsNullOrEmpty(record.RequestRaw))
            {
                builder.AppendLine("--- Request ---");
                builder.AppendLine(record.RequestRaw);
                builder.AppendLine();
            }
            if (!string.IsNullOrEmpty(record.ResponseRaw))
            {
                builder.AppendLine("--- Response ---");
                builder.AppendLine(record.ResponseRaw);
                builder.AppendLine();
            }
        }
        return builder.ToString();
    }
    public static string RenderReport(ChainScope scope, IReadOnlyDictionary<string, TimeSpan> durations)
    {
        var headers = new[] { "Index", "Name", "Kind", "Status", "Attempts", "Duration", "Note" };

        var builder = new StringBuilder();
        AppendCsvRow(builder, headers);

        var index = 0;
        foreach (var (name, record) in scope.Records)
        {
            index++;
            var note = (record.Note ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            var duration = durations.TryGetValue(name, out var d) ? d : TimeSpan.Zero;
            AppendCsvRow(builder, new[]
            {
                index.ToString(CultureInfo.InvariantCulture),
                name,
                record.Metadata.Kind.ToString(),
                record.Status.ToString(),
                record.Attempts.ToString(CultureInfo.InvariantCulture),
                FormatDuration(duration),
                note,
            });
        }
        return builder.ToString();
    }

    static void AppendField(StringBuilder builder, string label, string value)
    {
        builder.Append(label.PadRight(9)).Append(": ").AppendLine(value);
    }
    static void AppendCsvRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }
            builder.Append(EscapeCsv(cells[i]));
        }
        // RFC 4180 prefers CRLF line endings.
        builder.Append("\r\n");
    }
    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes)
        {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
