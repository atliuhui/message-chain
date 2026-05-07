using System.Globalization;
using System.Text;

namespace Cep;

static class DictionaryExtension
{
    static DictionaryExtension()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Attempts to assign a computed value to the dictionary and ignores invalid process-state failures.
    /// </summary>
    public static void TrySetValue(
        this IDictionary<string, string> dictionary,
        string key,
        Func<string> factory)
    {
        try
        {
            dictionary[key] = factory();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Gets a string value by key, or returns the provided default when the key is missing.
    /// </summary>
    public static string GetValueOrDefault(
        this IDictionary<string, string> dictionary,
        string key,
        string defaultValue = default!)
    {
        return dictionary.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }
    /// <summary>
    /// Gets a timeout value in seconds by key, or returns the provided default when missing or invalid.
    /// </summary>
    public static TimeSpan GetTimeSpanOrDefault(
        this IDictionary<string, string> dictionary,
        string key,
        TimeSpan defaultValue = default!)
    {
        return (dictionary.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            ? seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero
            : defaultValue;
    }
    /// <summary>
    /// Gets an encoding by key name, or returns the provided default when the key is missing.
    /// </summary>
    public static Encoding GetEncodingOrDefault(
        this IDictionary<string, string> dictionary,
        string key,
        Encoding defaultValue = default!)
    {
        return dictionary.TryGetValue(key, out var value)
            ? Encoding.GetEncoding(value)
            : defaultValue;
    }

    /// <summary>
    /// Gets the CEP working directory header value.
    /// </summary>
    public static string GetWorkingDirectory(
        this IDictionary<string, string> dictionary,
        string defaultValue = default!)
    {
        return GetValueOrDefault(dictionary, "Working-Directory", defaultValue);
    }
    /// <summary>
    /// Gets the CEP timeout header value.
    /// </summary>
    public static TimeSpan GetTimeout(
        this IDictionary<string, string> dictionary,
        TimeSpan defaultValue = default!)
    {
        return GetTimeSpanOrDefault(dictionary, "Timeout", defaultValue);
    }
    /// <summary>
    /// Gets the CEP charset header value.
    /// </summary>
    public static Encoding GetEncoding(
        this IDictionary<string, string> dictionary,
        Encoding defaultValue = default!)
    {
        return GetEncodingOrDefault(dictionary, "Charset", defaultValue);
    }
}
