using System.Text;
using System.Text.RegularExpressions;

namespace CepMessageFormats;

static class EnvironmentExtension
{
    static readonly Regex name_regex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Expands placeholders in the form <c>${NAME}</c> using the host process environment
    /// variables. Unknown or invalid placeholders are kept unchanged.
    /// </summary>
    /// <param name="text">Input text that may contain placeholders.</param>
    /// <returns>Text after placeholder expansion.</returns>
    public static string ExpandEnvironmentVariables(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        StringBuilder? builder = null;
        var span = text.AsSpan();
        var index = 0;
        while (index < span.Length)
        {
            var open = span[index..].IndexOf("${", StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            open += index; // absolute index
            var nameStart = open + 2;

            var close = span[nameStart..].IndexOf('}');
            if (close < 0)
            {
                break;
            }
            close += nameStart;

            // append prefix
            builder ??= new StringBuilder(text.Length);
            builder.Append(span[index..open]);

            var nameSpan = span[nameStart..close];
            if (name_regex.IsMatch(nameSpan))
            {
                var name = nameSpan.ToString();
                var value = Environment.GetEnvironmentVariable(name);
                if (value is not null)
                {
                    builder.Append(value);
                }
                else
                {
                    builder.Append(span[open..(close + 1)]); // keep ${NAME}
                }
            }
            else
            {
                // not a valid placeholder => keep as-is
                builder.Append(span[open..(close + 1)]);
            }

            index = close + 1;
        }

        if (builder is null)
        {
            return text;
        }
        builder.Append(span[index..]);

        return builder.ToString();
    }
}
