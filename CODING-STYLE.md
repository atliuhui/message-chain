# Coding Style

This document is the **single source of truth** for C# coding style in this repository. It follows the [dotnet/runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md) and the [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/).

> **Goal:** the `.editorconfig` at the repository root can be derived directly from this document. Every machine-enforceable rule is followed by an `.editorconfig` snippet inside an `ini` code block. Rules that cannot be encoded mechanically are marked **(doc-only)** and rely on review.

## Legend

- ⚙️ **Enforced via `.editorconfig`** — a snippet is provided; copy into `.editorconfig`.
- 📖 **(doc-only)** — convention to follow; reviewers must catch violations.

---

## 1. File Basics ⚙️

These settings define how every text file in the repository is encoded and indented. They apply to all languages — `.cs` source, project files, scripts, configs, and documentation — to keep diffs minimal and avoid mixed line endings or stray whitespace. C# files use 4-space indentation; lightweight markup formats (Markdown, YAML, JSON, XML, MSBuild, `.slnx`) use 2 spaces.

```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
indent_style = space
indent_size = 4
tab_width = 4
trim_trailing_whitespace = true
insert_final_newline = true

[*.{md,yml,yaml,json,xml,csproj,props,targets,slnx}]
indent_size = 2
```

---

## 2. Project & File Layout 📖

- Top-level folders: `src/` for production code, `test/` for tests.
- Project folder name = `csproj` name (e.g. `src/Mcf/Mcf.csproj`).
- Sub-folders inside a project use **plural PascalCase** by responsibility: `Handlers/`, `Models/`.
- Test project: `test/UnitTest/`. Test class name = type-under-test + aspect + `Test`, e.g. `ChainEngineRenderTest`, `HttpConverterTest`.
- One public type per file; the file name matches the type name.
- `partial` types are split by responsibility with the form `<TypeName>.<Aspect>.cs` (e.g. `ChainEngine.RunAsync.cs`, `ChainEngine.Render.cs`).
- Test method name follows `<MethodName>_<TestIntent>`, e.g. `RunAsync_ThrowsWhenChainIsNull`, `Parse_AcceptsEmptyHeaders`.
- Cross-file shared `using` directives go into a single `GlobalUsings.cs` per project.

---

## 3. Formatting

This section governs the visual layout of C# code: where braces and newlines go, how blocks and switch labels are indented, where spaces are required or forbidden, and how `using` directives are sorted and placed. The settings target the Roslyn / `dotnet format` formatter so that running `dotnet format` reproduces the exact layout described here without manual cleanup.

### 3.1 Braces and newlines ⚙️

Use Allman-style braces (every `{` on its own line) and require braces around every control-flow body, even single statements. This keeps diffs minimal when statements are added later and avoids the dangling-else / single-line-`if` ambiguities that lead to subtle bugs.

```ini
[*.cs]
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_between_query_expression_clauses = true

csharp_prefer_braces = true:warning
```

### 3.2 Indentation ⚙️

Indent block contents, `switch` labels and their bodies one level relative to the enclosing scope; align goto/case labels one level shallower than the surrounding statements. Braces themselves are not indented — they sit at the same column as the construct that owns them.

```ini
[*.cs]
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_labels = one_less_than_current
csharp_indent_block_contents = true
csharp_indent_braces = false
```

### 3.3 Spacing ⚙️

Keep spacing tight around parentheses (no padding inside `(...)` for either declarations or calls) and a single space after control-flow keywords like `if`, `for`, `while`. Binary operators are surrounded by spaces; casts have no trailing space. The colon in inheritance clauses gets a space on both sides for readability.

```ini
[*.cs]
csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_parentheses = false
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_around_binary_operators = before_and_after
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false
```

### 3.4 Imports (`using`) ⚙️

Place `using` directives **outside** the namespace declaration (consistent with file-scoped namespaces in §5.1) and sort `System.*` first; do not insert blank lines between import groups so the block stays compact and `dotnet format` can normalise it deterministically.

```ini
[*.cs]
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_using_directive_placement = outside_namespace:warning
```

---

## 4. Type Member Order 📖

> No `.editorconfig` option enforces member ordering — this section is review-only. The StyleCop analyzers (`SA1201`/`SA1202`/`SA1204`) can mechanise a similar ordering, but their categories do not match this document one-to-one and they are not enabled in this repository.

Inside a type, declare members in the following order:

1. Compile-time declarations (`[GeneratedRegex]` partial methods, etc.)
2. Constants (`const`)
3. Static fields (`static readonly` first, then mutable `static`)
4. Static constructor (`static TypeName()`)
5. Instance fields (`readonly` first, then mutable)
6. Internal / private properties
7. Public properties
8. Constructors (parameterless first, parameterized after)
9. `Dispose` / `DisposeAsync` / finalizer
10. `override` methods
11. Public methods (including `partial` declarations)
12. Private methods
13. Static methods
14. Nested types (`class`, `struct`, `enum`, `record`)

---

## 5. Language Style

This section codifies how C# language features are used in the codebase: which declaration shapes are preferred (file-scoped namespaces, `sealed` by default), how modifiers are ordered, when `var` is acceptable, where expression-bodied members are appropriate, and which modern syntax (pattern matching, null-coalescing, object initializers, etc.) is the idiomatic choice. Each rule below maps to one or more `.editorconfig` keys so violations surface as analyzer diagnostics.

### 5.1 Namespace and type declarations ⚙️

Use file-scoped namespaces (`namespace Foo;`) for every new file to reduce indentation. Concrete types not designed as base classes use `sealed class`; internal-only types omit the access modifier and rely on the default `internal`.

```ini
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
```

📖 Concrete types not designed as base classes use `sealed class`. Internal-only types omit the access modifier (default `internal`).

### 5.2 Modifiers ⚙️

Omit accessibility modifiers when they match the language default (e.g. `class Foo` instead of `internal class Foo`). Order modifiers consistently and mark fields `readonly` whenever they are not reassigned after construction.

```ini
[*.cs]
dotnet_style_require_accessibility_modifiers = omit_if_default:warning
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async:warning
dotnet_style_readonly_field = true:warning
```

### 5.3 `var` preferences ⚙️

Prefer `var` for every local variable — including built-in primitives (`var count = 0`), apparent types (`var client = new HttpClient()`), and any other local. Repeating the type on the left-hand side adds noise without information; `var` keeps declarations compact and refactor-friendly.

```ini
[*.cs]
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
```

### 5.4 Expression-bodied members ⚙️

Prefer expression bodies for trivial, single-line members (properties, accessors, lambdas, single-line methods/operators/local functions). Constructors stay block-bodied because they typically initialize multiple fields and benefit from explicit braces.

```ini
[*.cs]
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_constructors = false:suggestion
csharp_style_expression_bodied_operators = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_indexers = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion
csharp_style_expression_bodied_lambdas = true:suggestion
csharp_style_expression_bodied_local_functions = when_on_single_line:suggestion
```

### 5.5 Modern C# patterns ⚙️

Favour modern, expression-oriented syntax over older imperative forms: pattern matching instead of `is` + cast or `as` + null-check, inlined `out` declarations, throw expressions, null-conditional invocation, null-coalescing, null propagation, object/collection initializers, and auto-properties. Prefer conditional expressions over branched assignment or return when the result is a single value.

```ini
[*.cs]
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_style_inlined_variable_declaration = true:suggestion
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

dotnet_style_coalesce_expression = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_prefer_auto_properties = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:suggestion
dotnet_style_prefer_conditional_expression_over_return = true:suggestion
```

### 5.6 Modifier order ⚙️

When a member carries multiple modifiers, declare them in the exact order below. This is the default value of `csharp_preferred_modifier_order` (rule **IDE0036**) and matches the value already shown in §5.2.

```text
public → private → protected → internal → file →
static → extern → new →
virtual → abstract → sealed → override →
readonly →
unsafe → required → volatile →
async
```

Examples:

```csharp
public static readonly HttpClient DefaultClient = new();
protected internal sealed override Task RunAsync() => Task.CompletedTask;
private static async Task<int> ParseAsync() => 0;
```

```ini
[*.cs]
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async:warning
```

---

## 6. Naming

### 6.1 Casing styles ⚙️

The conventions in §6.2 reference the following base styles. Compound styles add a fixed prefix on top of one of these (e.g. `_` + camelCase, `s_` + camelCase, `I` + PascalCase).

| Style | Definition | Example |
|---|---|---|
| `PascalCase` | Each word capitalised, no separators | `ChainEngine`, `RunAsync` |
| `camelCase` | First word lowercase, subsequent words capitalised, no separators | `cancellationToken`, `httpClient` |
| `snake_case` | All lowercase, words separated by `_` | `default_http_client` |
| `UPPER_CASE` | All uppercase, words separated by `_` | `JSON_CACHE_KEY`, `MAX_STEPS` |

> This repository uses **`PascalCase`**, **`camelCase`**, **`snake_case`**, and **`UPPER_CASE`**.

```ini
[*.cs]
# ── styles ───────────────────────────────────────────────
dotnet_naming_style.pascal_case.capitalization = pascal_case

dotnet_naming_style.camel_case.capitalization = camel_case

dotnet_naming_style.snake_case.capitalization = all_lower
dotnet_naming_style.snake_case.word_separator = _

dotnet_naming_style.upper_case.capitalization = all_upper
dotnet_naming_style.upper_case.word_separator = _

dotnet_naming_style.i_prefix.capitalization = pascal_case
dotnet_naming_style.i_prefix.required_prefix = I

dotnet_naming_style.t_prefix.capitalization = pascal_case
dotnet_naming_style.t_prefix.required_prefix = T

dotnet_naming_style.async_suffix.capitalization = pascal_case
dotnet_naming_style.async_suffix.required_suffix = Async
```

### 6.2 Element-by-element rules ⚙️

"Visibility" lists the modifiers the rule applies to; **Private** here covers `private`, `internal`, `private protected`, and `file`-local declarations.

| Element | Visibility | Convention | Example |
|---|---|---|---|
| Assembly / NuGet package | — | PascalCase, dot-separated | `Mcf.Core` |
| Namespace | — | PascalCase, dot-separated | `Mcf.Handlers` |
| Class | Public / Private | PascalCase | `ChainEngine`, `HttpStepHandler` |
| Abstract class | Public / Private | PascalCase | `StepHandler` |
| Static class | Public / Private | PascalCase | `JsonExtensions` |
| Sealed class | Public / Private | PascalCase | `RetryPolicy` |
| Struct | Public / Private | PascalCase | `StepKind` |
| Record / record struct | Public / Private | PascalCase | `StepHandlerRegistration` |
| Interface | Any | IPascalCase | `IStepHandler` |
| Enum type | Public / Private | PascalCase, **singular** noun | `StepStatus` |
| Enum member | — | PascalCase | `StepStatus.Success` |
| `[Flags]` enum type | Public / Private | PascalCase, **plural** noun | `FileAccessRights` |
| `[Flags]` enum member | — | PascalCase | `FileAccessRights.Read` |
| Delegate | Public / Private | PascalCase, ends with verb / `Handler` | `FilterDelegate`, `StepCompletedHandler` |
| Event | Public / Private | PascalCase, verb tense conveys timing | `Started`, `Completing` |
| Generic type parameter | — | TPascalCase (single descriptive word) | `TResult`, `TStepHandler` |
| Attribute class | Public / Private | PascalCase, ends with `Attribute` | `StepKindAttribute` |
| Exception class | Public / Private | PascalCase, ends with `Exception` | `ChainExecutionException` |
| Constructor / finalizer | — | Same as type name | `ChainEngine()`, `~ChainEngine()` |
| Sync method | Public / Protected | PascalCase, verb-first | `Render`, `Execute` |
| Async method | Public / Protected | PascalCaseAsync, verb-first | `RunAsync`, `ParseRequestAsync` |
| Method | Private | PascalCase, verb-first | `IsExpectedCode`, `MapRequest` |
| Local function | — | PascalCase | `bool TryParse(...)` |
| Property | Public / Protected | PascalCase | `Metadata`, `HttpClient` |
| Property | Private | PascalCase | `CurrentStep` |
| Indexer | Any | `this[...]` | `this[int index]` |
| Auto-property backing field | — | Compiler-generated; do not rename |  |
| `const` | Any | UPPER_CASE | `JSON_CACHE_KEY`, `MAX_STEPS` |
| `static readonly` field | Private | snake_case | `default_http_client` |
| Mutable `static` field | Public / Protected | PascalCase (avoid; prefer property) | `Current` |
| Mutable `static` field | Private | snake_case | `json_path_cache` |
| `[ThreadStatic]` field | Private | snake_case | `current_context` |
| Instance field (`readonly`) | Private | snake_case | `http_client` |
| Instance field (mutable) | Private | snake_case | `running_flag` |
| Instance field | Public / Protected | **Forbidden** — use a property instead | — |
| Method parameter | — | camelCase | `cancellationToken`, `source` |
| Optional parameter (default value) | — | camelCase | `string operation = ""` |
| `out` / `ref` / `in` parameter | — | camelCase | `out var registration` |
| `params` parameter | — | camelCase, plural | `params string[] arguments` |
| Local variable | — | camelCase | `var stepCancellation = ...` |
| Range / loop variable | — | camelCase (`i` / `j` / `k` only inside short numeric loops) | `foreach (var step in ...)` |
| Tuple element name | — | PascalCase | `(int Min, int Max)`, `(string ResponseRaw, int Code)` |
| Anonymous-type member | — | PascalCase | `new { FullName = ... }` |
| Pattern-match designation | — | camelCase | `is StepRecord record` |
| Lambda parameter | — | camelCase, single short word | `step => step.Status` |
| Discard | — | `_` (single underscore) | `_ = TryGet(...)` |
| XML doc parameter reference | — | Match the parameter name exactly | `<paramref name="cancellationToken"/>` |

```ini
[*.cs]
# ── symbols ──────────────────────────────────────────────
dotnet_naming_symbols.namespaces.applicable_kinds = namespace
dotnet_naming_symbols.types.applicable_kinds = class,struct,enum,delegate
dotnet_naming_symbols.records.applicable_kinds = struct,class
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter

dotnet_naming_symbols.public_members.applicable_kinds = property,method,event,field
dotnet_naming_symbols.public_members.applicable_accessibilities = public,protected,protected_internal,private_protected

dotnet_naming_symbols.constants.applicable_kinds = field,local
dotnet_naming_symbols.constants.required_modifiers = const

dotnet_naming_symbols.static_readonly_fields.applicable_kinds = field
dotnet_naming_symbols.static_readonly_fields.applicable_accessibilities = public,protected,protected_internal
dotnet_naming_symbols.static_readonly_fields.required_modifiers = static,readonly

dotnet_naming_symbols.private_static_fields.applicable_kinds = field
dotnet_naming_symbols.private_static_fields.applicable_accessibilities = private,internal,private_protected
dotnet_naming_symbols.private_static_fields.required_modifiers = static

dotnet_naming_symbols.private_instance_fields.applicable_kinds = field
dotnet_naming_symbols.private_instance_fields.applicable_accessibilities = private,internal,private_protected

dotnet_naming_symbols.locals_and_parameters.applicable_kinds = parameter,local

# ── rules (severity = warning) ───────────────────────────
dotnet_naming_rule.namespaces_pascal.severity = warning
dotnet_naming_rule.namespaces_pascal.symbols  = namespaces
dotnet_naming_rule.namespaces_pascal.style    = pascal_case

dotnet_naming_rule.types_pascal.severity = warning
dotnet_naming_rule.types_pascal.symbols  = types
dotnet_naming_rule.types_pascal.style    = pascal_case

dotnet_naming_rule.interfaces_i_prefix.severity = warning
dotnet_naming_rule.interfaces_i_prefix.symbols  = interfaces
dotnet_naming_rule.interfaces_i_prefix.style    = i_prefix

dotnet_naming_rule.type_parameters_t_prefix.severity = warning
dotnet_naming_rule.type_parameters_t_prefix.symbols  = type_parameters
dotnet_naming_rule.type_parameters_t_prefix.style    = t_prefix

dotnet_naming_rule.public_members_pascal.severity = warning
dotnet_naming_rule.public_members_pascal.symbols  = public_members
dotnet_naming_rule.public_members_pascal.style    = pascal_case

dotnet_naming_rule.constants_upper.severity = warning
dotnet_naming_rule.constants_upper.symbols  = constants
dotnet_naming_rule.constants_upper.style    = upper_case

dotnet_naming_rule.static_readonly_pascal.severity = warning
dotnet_naming_rule.static_readonly_pascal.symbols  = static_readonly_fields
dotnet_naming_rule.static_readonly_pascal.style    = pascal_case

dotnet_naming_rule.private_static_snake.severity = warning
dotnet_naming_rule.private_static_snake.symbols  = private_static_fields
dotnet_naming_rule.private_static_snake.style    = snake_case

dotnet_naming_rule.private_instance_snake.severity = warning
dotnet_naming_rule.private_instance_snake.symbols  = private_instance_fields
dotnet_naming_rule.private_instance_snake.style    = snake_case

dotnet_naming_rule.locals_camel.severity = suggestion
dotnet_naming_rule.locals_camel.symbols  = locals_and_parameters
dotnet_naming_rule.locals_camel.style    = camel_case
```

> Rule precedence: more specific rules are evaluated first. Place `private_static_*` before `private_instance_*` so static fields match the `snake_case` style; inside `.editorconfig`, ordering of rules controls precedence.

### 6.3 Semantic naming 📖

- **Collections** use plural names: `Steps`, `RetryDelays`, `Headers`.
- **Booleans** use `Is` / `Has` / `Can` / `Should` prefix: `IsSuccessCode`, `HasError`. Domain-clear names like `ContinueOnError` are acceptable.
- **Counts and quantities** carry explicit semantics: `MaxRetries`, `RetryAttempts`. Avoid using a verb (`Retry`) as a count.
- **`[GeneratedRegex]` partial methods** use a `<Subject>Regex` PascalCase name describing what the pattern matches: `MetadataLineRegex`, `VariableLineRegex`, `VariableNameRegex`. The `Regex` suffix is preserved as a single word (per the acronym rule below).
- **Use whole words or noun phrases that convey meaning** — never opaque single letters or truncated stems. Identifiers should read like prose so a reviewer can guess the role without inspecting the surrounding code.
  - Avoid common-word abbreviations: `metadata` not `meta`, `variables` not `vars`, `cancellationToken` not `ct`, `builder` not `sb`, `request` not `req`, `response` not `res`/`resp`, `dictionary` not `dict`, `command` not `cmd`, `arguments` not `args` (parameter `params string[] arguments` is fine; the local alias `args` is not), `configuration` not `cfg`/`conf`, `temporary` not `tmp`, `directory` not `dir`, `index` not `idx`, `count` not `cnt`, `length` not `len`, `position` not `pos`, `buffer` not `buf`, `cursor` not `cur`, `value` not `val`, `error` not `err`, `exception` not `ex` (only `catch (Exception ex)` is conventional), `message` not `msg`, `result` not `res`/`r`.
  - Avoid single-letter locals (`s`, `b`, `t`, `x`, `y`) outside the narrow exceptions below; prefer the noun they represent (`span`, `builder`, `text`, `node`).
  - **Allowed single-letter exceptions** (kept short by long-standing convention):
    - `i` / `j` / `k` — index inside a short numeric `for` loop (`for (var i = 0; i < count; i++)`). Do **not** use them as a long-lived cursor across a multi-step algorithm; rename to `cursor`, `position`, etc.
    - `e` — event-args parameter inside a small event handler (`(_, e) => ...`).
    - `ex` — caught exception inside `catch (Exception ex)`.
    - `_` — discard.
  - Domain acronyms that are themselves the noun (`url`, `id`, `uri`, `db`, `io`) are not abbreviations — they are the actual word — and follow §6.3's acronym rule (`Url`, not `URL`).
- Well-known acronyms (`Http`, `Cep`, `Json`, `Url`, `Id`) are treated as one word: `HttpClient`, not `HTTPClient`.
- Async methods returning `Task` / `Task<T>` / `ValueTask` use the `Async` suffix: `RunAsync`, `ParseRequestAsync`. (Optionally enforced by the **VSTHRD200** analyzer if the team adds `Microsoft.VisualStudio.Threading.Analyzers`.)

---

## 7. API Conventions 📖

- A `CancellationToken` parameter is the **last** parameter; public APIs give it a default value (`CancellationToken cancellationToken = default`). For the parameter name itself, see §6.2.
- In library code, `await` every awaited task with `.ConfigureAwait(false)`.
- Throw early via `ArgumentNullException.ThrowIfNull(arg)` and `ArgumentOutOfRangeException` factories.
- Prefer `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey,TValue>` on public surface; use `Array.Empty<T>()` for empty defaults.

---

## 8. Comments & Documentation 📖

- Public types and public members have XML doc comments (`/// <summary>`, `<param>`, `<returns>`, `<see cref="…"/>`).
- Use `<para>` for multi-paragraph summaries.
- Inline `//` comments wrap around 72–80 columns; explain *why*, not *what*.
- Do not commit commented-out code.

---

## 9. Quick Reference

A condensed cheat-sheet of the most common discouraged → preferred transformations.

| Discouraged | Preferred | Note |
|---|---|---|
| `using` inside namespace | `using` outside namespace | Place `using` at the top of the file, above the namespace declaration. |
| `namespace Foo { ... }` (block) | `namespace Foo;` (file-scoped) | One namespace per file; reduces indentation. |
| `class RetryPolicy` (concrete, non-base) | `sealed class RetryPolicy` | Mark concrete types `sealed` unless intentionally designed for inheritance. |
| `internal class Foo` | `class Foo` (default `internal`) | Omit accessibility modifiers when they match the language default. |
| `HttpClient http_client;` (never reassigned) | `readonly HttpClient http_client;` | Use `readonly` for fields not reassigned after construction. |
| `static public readonly ...` | `public static readonly ...` | Follow the canonical modifier order (see §5.6). |
| `HttpClient client = new HttpClient();` | `var client = new HttpClient();` | Use `var` for every local — including built-in primitives. |
| `int count = 0;` | `var count = 0;` | Prefer `var` even for primitives; the literal makes the type obvious. |
| `if (x is string) { var s = (string)x; ... }` | `if (x is string s) { ... }` | Use pattern matching instead of `is` + cast. |
| `if (x != null) x.Run();` | `x?.Run();` | Use null-conditional invocation. |
| `var v = x != null ? x : fallback;` | `var v = x ?? fallback;` | Use the null-coalescing operator. |
| `const int MaxSteps = 8;` | `const int MAX_STEPS = 8;` | `const` uses `UPPER_CASE`. |
| `private static FluidParser TemplateParser;` | `static readonly FluidParser template_parser;` | Private static fields use `snake_case`; prefer `readonly`. |
| `private HttpClient httpClient;` | `readonly HttpClient http_client;` | Private instance fields use `snake_case`. |
| `public int Count;` | `public int Count { get; }` (property) | Public mutable fields are forbidden — use a property. |
| `StepHandler` (interface) | `IStepHandler` | Interfaces use the `I` prefix. |
| `class Cache<Result>` | `class Cache<TResult>` | Generic type parameters use the `T` prefix. |
| `Task Run()` | `Task RunAsync()` | Methods returning `Task`/`Task<T>`/`ValueTask` use the `Async` suffix. |
| `public void Step()` | `public void RunStep()` | Method names start with a verb. |
| `bool Success;` | `bool IsSuccess;` | Booleans use `Is`/`Has`/`Can`/`Should` prefix. |
| `public int Retry { get; set; }` | `public int RetryAttempts { get; set; }` | Counts/quantities carry explicit semantics — avoid bare verbs. |
| `IReadOnlyList<TimeSpan> RetryDelay` | `IReadOnlyList<TimeSpan> RetryDelays` | Collection names are plural. |
| `var ct = ...;` / `var meta = ...;` | `var cancellationToken = ...;` / `var metadata = ...;` | Avoid abbreviations for common words. |
| `HTTPClient`, `JSONParser` | `HttpClient`, `JsonParser` | Treat well-known acronyms as a single word. |
| `RunAsync(CancellationToken ct, string url)` | `RunAsync(string url, CancellationToken cancellationToken = default)` | `CancellationToken` is the last parameter and defaults to `default` on public APIs. |
| `await task;` | `await task.ConfigureAwait(false);` | In library code, suppress context capture on every awaited task. |
| `if (arg == null) throw new ArgumentNullException();` | `ArgumentNullException.ThrowIfNull(arg);` | Use the throw-helper factories for argument validation. |
| `List<T>` on public API | `IReadOnlyList<T>` | Expose read-only abstractions on the public surface. |

---

## 10. How to Apply

1. Run `dotnet format` at the repository root to apply formatting and naming fixes.

   ```powershell
   # Apply all fixes (whitespace, style, analyzers) across the solution
   dotnet format MessageChain.slnx

   # Apply only one category at a time
   dotnet format MessageChain.slnx --severity warn whitespace
   dotnet format MessageChain.slnx --severity warn style
   dotnet format MessageChain.slnx --severity warn analyzers

   # CI gate: fail if any file would be changed
   dotnet format MessageChain.slnx --verify-no-changes --severity warn

   # Scope to a single project or file
   dotnet format src/Mcf/Mcf.csproj
   dotnet format MessageChain.slnx --include src/Mcf/ChainEngine.cs
   ```

2. Treat warnings emitted by `.editorconfig` rules as build issues; raise severity to `error` once the codebase is clean.

   ```powershell
   # Surface style/analyzer diagnostics during a normal build
   dotnet build MessageChain.slnx /warnaserror

   # Inspect a specific rule (e.g. naming) without applying fixes
   dotnet format MessageChain.slnx --diagnostics IDE1006 --verify-no-changes
   ```

3. Optional analyzers worth adding via NuGet:

   ```powershell
   dotnet add src/Mcf/Mcf.csproj package Microsoft.CodeAnalysis.NetAnalyzers
   dotnet add src/Mcf/Mcf.csproj package Microsoft.VisualStudio.Threading.Analyzers
   ```

   - `Microsoft.CodeAnalysis.NetAnalyzers` — CAxxxx rules.
   - `Microsoft.VisualStudio.Threading.Analyzers` — Async-suffix (VSTHRD200) and threading rules.

---

## 11. References

- [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- [Framework Design Guidelines — Naming](https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-guidelines)
- [EditorConfig — .NET options reference](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options)
- [EditorConfig — naming rules reference](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/naming-rules)
- [IDE0036 — Order modifiers](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0036)
