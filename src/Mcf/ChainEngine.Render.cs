using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Json.Path;

namespace Mcf;

public partial class ChainEngine
{
    const string JSON_CACHE_KEY = "__json_cache__";
    const string JSON_TOKEN_MAP_KEY = "__json_token_map__";

    static readonly FluidParser template_parser = new();
    static readonly ConcurrentDictionary<string, JsonPath> json_path_cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Liquid <see cref="TemplateOptions"/> used by this engine. Pre-configured
    /// with built-in filters (<c>parse_json</c>, <c>json_path</c>,
    /// <c>parse_json_path</c>) and member-access registrations for
    /// <see cref="StepRecord"/> / <see cref="StepMetadata"/> / <see cref="ExchangeMessage"/>.
    /// <para>
    /// Mutate this instance to register additional Liquid filters, value
    /// converters, or member access strategies before calling
    /// <see cref="RunAsync"/>. Each <see cref="ChainEngine"/> owns its own
    /// options, so modifications are local to the instance.
    /// </para>
    /// </summary>
    public TemplateOptions FluidOptions { get; } = CreateTemplateOptions();

    /// <summary>
    /// Convenience wrapper that registers a Liquid filter on
    /// <see cref="FluidOptions"/>. Equivalent to
    /// <c>FluidOptions.Filters.AddFilter(name, filter)</c>.
    /// </summary>
    public void RegisterFluidFilter(string name, FilterDelegate filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(filter);
        FluidOptions.Filters.AddFilter(name, filter);
    }

    static TemplateOptions CreateTemplateOptions()
    {
        var options = new TemplateOptions
        {
            ModelNamesComparer = StringComparer.OrdinalIgnoreCase,
        };
        options.MemberAccessStrategy.IgnoreCasing = true;
        options.MemberAccessStrategy.Register<StepRecord>();
        options.MemberAccessStrategy.Register<StepMetadata>();
        options.MemberAccessStrategy.Register<ExchangeMessage>();
        options.ValueConverters.Add(value => value is Enum e ? e.ToString() : null);
        options.Filters.AddFilter("parse_json", ParseJsonFilter);
        options.Filters.AddFilter("json_path", JsonPathFilter);
        options.Filters.AddFilter("parse_json_path", ParseJsonPathFilter);
        options.Filters.AddFilter("env", EnvFilter);
        options.Filters.AddFilter("is_ok_status", IsOkStatusFilter);
        options.Filters.AddFilter("not", NotFilter);
        options.Filters.AddFilter("file_exists", FileExistsFilter);
        options.Filters.AddFilter("dir_exists", DirExistsFilter);
        return options;
    }

    /// <summary>
    /// Liquid filter <c>dir_exists</c>: returns <c>true</c> when the input
    /// string is a path to an existing directory on the host filesystem, and
    /// <c>false</c> otherwise (including when the input is <c>nil</c>, empty,
    /// or points to a file). Relative paths are resolved against the current
    /// working directory.
    /// Example: <c>{% if "./logs" | dir_exists %}...{% endif %}</c>.
    /// </summary>
    static ValueTask<FluidValue> DirExistsFilter(FluidValue input, FilterArguments _, TemplateContext __)
    {
        var path = input?.ToStringValue();
        var exists = !string.IsNullOrEmpty(path) && Directory.Exists(path);
        return new ValueTask<FluidValue>(BooleanValue.Create(exists));
    }

    /// <summary>
    /// Liquid filter <c>file_exists</c>: returns <c>true</c> when the input
    /// string is a path to an existing file on the host filesystem, and
    /// <c>false</c> otherwise (including when the input is <c>nil</c>, empty,
    /// or points to a directory). Relative paths are resolved against the
    /// current working directory.
    /// Example: <c>{% if "./config.json" | file_exists %}...{% endif %}</c>.
    /// </summary>
    static ValueTask<FluidValue> FileExistsFilter(FluidValue input, FilterArguments _, TemplateContext __)
    {
        var path = input?.ToStringValue();
        var exists = !string.IsNullOrEmpty(path) && File.Exists(path);
        return new ValueTask<FluidValue>(BooleanValue.Create(exists));
    }

    /// <summary>
    /// Liquid filter <c>not</c>: returns the boolean negation of the input
    /// using standard Liquid truthiness — only <c>nil</c> and the boolean
    /// <c>false</c> are falsy; every other value (including empty strings,
    /// <c>0</c>, and empty collections) is truthy.
    /// Example: <c>{% assign ok = login.status | is_ok_status %}{% if ok | not %}failed{% endif %}</c>.
    /// </summary>
    static ValueTask<FluidValue> NotFilter(FluidValue input, FilterArguments _, TemplateContext __)
    {
        var truthy = input is not null && input.ToBooleanValue();
        return new ValueTask<FluidValue>(BooleanValue.Create(!truthy));
    }

    /// <summary>
    /// Liquid filter <c>is_ok_status</c>: returns <c>true</c> when the input
    /// represents a <see cref="StepStatus"/> of <see cref="StepStatus.Success"/>
    /// or <see cref="StepStatus.Skipped"/>, and <c>false</c> otherwise. Accepts
    /// either a <see cref="StepStatus"/> enum value or its string form
    /// (case-insensitive), which is what <c>step.status</c> renders as.
    /// Example: <c>{% assign ok = login.status | is_ok_status %}{% if ok %}...{% endif %}</c>.
    /// </summary>
    static ValueTask<FluidValue> IsOkStatusFilter(FluidValue input, FilterArguments _, TemplateContext __)
    {
        var raw = input?.ToObjectValue();
        var ok = raw switch
        {
            StepStatus s => s is StepStatus.Success or StepStatus.Skipped,
            string text => string.Equals(text, nameof(StepStatus.Success), StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, nameof(StepStatus.Skipped), StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
        return new ValueTask<FluidValue>(BooleanValue.Create(ok));
    }

    /// <summary>
    /// Liquid filter <c>env</c>: returns the value of the host process
    /// environment variable named by the input string. When the variable is
    /// not set, returns the first filter argument as a fallback, or
    /// <see cref="NilValue.Instance"/> when no fallback is supplied (so the
    /// standard <c>default</c> filter can take over).
    /// Example: <c>{{ "BASE_URL" | env: "https://localhost" }}</c> or
    /// <c>{{ "BASE_URL" | env | default: "https://localhost" }}</c>.
    /// </summary>
    static ValueTask<FluidValue> EnvFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var name = input.ToStringValue();
        if (string.IsNullOrEmpty(name))
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        var value = Environment.GetEnvironmentVariable(name);
        if (value is not null)
        {
            return new ValueTask<FluidValue>(FluidValue.Create(value, context.Options));
        }

        if (arguments.Count > 0)
        {
            return new ValueTask<FluidValue>(arguments.At(0));
        }

        return new ValueTask<FluidValue>(NilValue.Instance);
    }

    /// <summary>
    /// Liquid filter <c>parse_json</c>: parses the input string as JSON and
    /// returns a navigable object usable in subsequent template expressions.
    /// The parsed token is cached per template context.
    /// Example: <c>{% assign data = str | parse_json %}{{ data.items[0].name }}</c>.
    /// </summary>
    static ValueTask<FluidValue> ParseJsonFilter(FluidValue input, FilterArguments _, TemplateContext context)
    {
        var source = input.ToStringValue();
        if (string.IsNullOrWhiteSpace(source))
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }
        var entry = GetOrParseJson(context, source);
        var fluid = FluidValue.Create(entry.Object, context.Options);
        // Register the produced FluidValue so a later `| json_path` (after an
        // `assign` or other passthrough) can recover the original JsonNode.
        GetOrCreateTokenMap(context)[fluid] = entry.Token;
        return new ValueTask<FluidValue>(fluid);
    }
    /// <summary>
    /// Liquid filter <c>parse_json_path</c>: parses the input string as JSON and
    /// evaluates the argument as a standard JSONPath expression.
    /// Shares the parsed-document cache with <c>parse_json</c>.
    /// Example: <c>{{ str | parse_json_path: '$.data.items[0].name' }}</c>.
    /// </summary>
    static ValueTask<FluidValue> ParseJsonPathFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var source = input.ToStringValue();
        if (string.IsNullOrWhiteSpace(source))
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        var entry = GetOrParseJson(context, source);
        var path = arguments.Count > 0 ? arguments.At(0).ToStringValue() : string.Empty;
        if (!TryResolveJsonPath(entry, path, context.Options, out var value))
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }
        return new ValueTask<FluidValue>(value);
    }
    /// <summary>
    /// Liquid filter <c>json_path</c>: evaluates a JSONPath expression against the
    /// input value previously produced by <c>parse_json</c> in the same render.
    /// Operates on the cached <see cref="JsonNode"/> so no re-parsing occurs.
    /// </summary>
    /// <remarks>
    /// The input must be the root value returned directly by <c>parse_json</c>
    /// in the current <see cref="TemplateContext"/>. Sub-objects produced via
    /// member access (e.g. <c>data.user</c>) are not tracked and will yield
    /// <c>nil</c>; for those cases prefer member access combined with
    /// <c>parse_json_path</c> on the original source string. The association is
    /// kept in <see cref="TemplateContext.AmbientValues"/> and does not survive
    /// across renders.
    /// Example: <c>{% assign data = str | parse_json %}{{ data | json_path: '$.items[*].name' }}</c>.
    /// </remarks>
    static ValueTask<FluidValue> JsonPathFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (input is null || input is NilValue)
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        if (!TryGetTokenMap(context, out var map) || !map.TryGetValue(input, out var token))
        {
            // The input was not produced by parse_json in this render, so there
            // is no associated JsonNode to evaluate JSONPath against.
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        var entry = new JsonCacheEntry(token, input.ToObjectValue());
        var path = arguments.Count > 0 ? arguments.At(0).ToStringValue() : string.Empty;
        if (!TryResolveJsonPath(entry, path, context.Options, out var value))
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }
        return new ValueTask<FluidValue>(value);
    }

    /// <summary>
    /// Returns a cached <see cref="JsonCacheEntry"/> for <paramref name="source"/>, parsing it
    /// once per <see cref="TemplateContext"/>. The entry holds both the <see cref="JsonNode"/>
    /// (for JSONPath evaluation) and the materialized object graph (for Fluid binding),
    /// so repeated references within the same template render avoid redundant work.
    /// </summary>
    static JsonCacheEntry GetOrParseJson(TemplateContext context, string source)
    {
        if (!context.AmbientValues.TryGetValue(JSON_CACHE_KEY, out var raw) ||
            raw is not Dictionary<string, JsonCacheEntry> cache)
        {
            // Use reference equality: in a single render the same source string
            // (e.g. step content) is normally the exact same instance, so this
            // gives near-zero-cost lookups and avoids comparing potentially
            // large JSON payloads character by character.
            cache = new Dictionary<string, JsonCacheEntry>(ReferenceEqualityComparer.Instance);
            context.AmbientValues[JSON_CACHE_KEY] = cache;
        }
        if (!cache.TryGetValue(source, out var entry))
        {
            var token = JsonNode.Parse(source);
            entry = new JsonCacheEntry(token, JsonNodeToObject(token));
            cache[source] = entry;
        }
        return entry;
    }
    static Dictionary<FluidValue, JsonNode?> GetOrCreateTokenMap(TemplateContext context)
    {
        if (!context.AmbientValues.TryGetValue(JSON_TOKEN_MAP_KEY, out var raw) ||
            raw is not Dictionary<FluidValue, JsonNode?> map)
        {
            map = new Dictionary<FluidValue, JsonNode?>(ReferenceEqualityComparer.Instance);
            context.AmbientValues[JSON_TOKEN_MAP_KEY] = map;
        }
        return map;
    }
    static bool TryGetTokenMap(TemplateContext context, out Dictionary<FluidValue, JsonNode?> map)
    {
        if (context.AmbientValues.TryGetValue(JSON_TOKEN_MAP_KEY, out var raw) &&
            raw is Dictionary<FluidValue, JsonNode?> existing)
        {
            map = existing;
            return true;
        }
        map = default!;
        return false;
    }
    static bool TryResolveJsonPath(JsonCacheEntry entry, string path, TemplateOptions options, out FluidValue result)
    {
        result = NilValue.Instance;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                result = FluidValue.Create(entry.Object, options);
                return true;
            }

            var jsonPath = GetOrParseJsonPath(path);
            var matches = jsonPath.Evaluate(entry.Token).Matches;
            if (matches.Count == 0)
            {
                return false;
            }

            if (matches.Count == 1)
            {
                result = FluidValue.Create(JsonNodeToObject(matches[0].Value), options);
                return true;
            }

            var list = matches.Select(match => JsonNodeToObject(match.Value)).ToList();
            result = FluidValue.Create(list, options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (PathParseException)
        {
            return false;
        }
    }
    static JsonPath GetOrParseJsonPath(string path) =>
        json_path_cache.GetOrAdd(path, static p => JsonPath.Parse(p));
    sealed record JsonCacheEntry(JsonNode? Token, object? Object);
    static object? JsonNodeToObject(JsonNode? token) => token switch
    {
        null => null,
        JsonObject obj => obj.ToDictionary(kvp => kvp.Key, kvp => JsonNodeToObject(kvp.Value), StringComparer.Ordinal),
        JsonArray array => array.Select(JsonNodeToObject).ToList(),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            // Prefer long for whole numbers (exact integer arithmetic in Fluid),
            // fall back to double to mirror Newtonsoft's JTokenType.Float => double.
            // Magnitudes that overflow double degrade to raw text so rendering
            // never throws on edge inputs.
            JsonValueKind.Number =>
                value.TryGetValue<long>(out var longValue) ? longValue
                    : value.TryGetValue<double>(out var doubleValue) ? doubleValue
                    : (object)value.ToJsonString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToJsonString(),
        },
        _ => token.ToJsonString(),
    };
    /// <summary>
    /// Renders a Liquid template using the current <see cref="Scope"/> as the
    /// template context. Intended primarily for testing the template/scope
    /// integration without running a full chain.
    /// </summary>
    static string RenderTemplate(string source, TemplateContext context)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }
        if (!template_parser.TryParse(source, out var template, out var error))
        {
            throw new InvalidOperationException($"Liquid parse error: {error}");
        }
        return template.Render(context);
    }
    internal string RenderTemplate(string source) => RenderTemplate(source, BuildTemplateContext());
    TemplateContext BuildTemplateContext()
    {
        var context = new TemplateContext(FluidOptions);
        foreach (var (name, value) in Scope.Variables)
        {
            context.SetValue(name, value);
        }
        foreach (var (name, record) in Scope.Records)
        {
            context.SetValue(name, record);
        }
        return context;
    }
}
