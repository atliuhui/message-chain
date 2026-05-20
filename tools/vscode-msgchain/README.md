# Message Chain Format (MCF) — VS Code Extension

Syntax highlighting for `.mcf` (Message Chain Format), `.cep` (Command Execution Protocol), `.http` / `.rest`, and `.liquid` files.

## Features (v0.1)

- Language registration for `.mcf` (`mcf`), `.cep` (`cep`), `.http` / `.rest` (`http`), `.liquid` (`liquid`).
- TextMate grammars for:
  - **MCF** (`source.mcf`) — `###` step separators, step metadata (`# @name|kind|when|timeout|retry-attempts|retry-delays|expect-codes|continue-on-error|description`), top/step-level variables (`@name = value`), HTTP request line, CEP `EXEC ... CEP/0.1` (and `SHELL` / `RUN` / `CALL`) start-line, headers, and embedded Liquid `{{ }}` / `{% %}`.
  - **CEP** (`source.cep`) — comments (`#`), request start-line (`EXEC <command> CEP/0.1`), response status-line (`CEP/0.1 <code> <reason>`), headers, named/token arguments, `${ENV_VAR}` placeholders.
  - **HTTP** (`source.http.mcf`) — `###` request separators, comments (`#` / `//`), `@name = value` variables, request line, headers, and embedded Liquid `{{ }}` / `{% %}` placeholders.
  - **Liquid** (`source.liquid`) — standalone grammar for `.liquid` files and shared injection grammar for MCF and HTTP. Covers raw / comment blocks, tags, output expressions, filters, strings, numbers, ranges, and operators.
- Scope names follow the project's data model (e.g. `meta.step.metadata.line.mcf`, `meta.exchange.request.line.http.mcf`, `meta.request.start-line.cep`, `meta.response.status-line.cep`) while keeping standard TextMate prefixes so VS Code's default themes still apply.
- Language configuration for all three languages:
  - `#` line comments (plus `//` in HTTP).
  - Auto-closing / surrounding pairs for `{{ }}`, `{% %}`, `${ }` (CEP), `{ }`, `[ ]`, `" "`.
  - Folding markers at every `###` separator.
- Snippets for MCF:
  - Step templates: `step-http`, `step-cep`, `step-empty`.
  - One snippet per metadata key: `@name`, `@kind`, `@when`, `@timeout`, `@retry-attempts`, `@retry-delays`, `@expect-codes`, `@continue-on-error`, `@description`.

> Note: the `http` language id may collide with other extensions (e.g. REST Client). If both are installed, VS Code will pick one; you can override per-file via the language selector in the status bar.

## Run locally

From the extension folder:

```powershell
code --extensionDevelopmentPath="$PWD"
```

Or press `F5` in this folder to launch an Extension Development Host, then open any `.mcf`, `.cep`, or `.http` / `.rest` file.

## Package

```powershell
npm install -g @vscode/vsce
vsce package
```

This produces a `.vsix` file you can install via `code --install-extension <file>.vsix`.

## Roadmap

- Document symbols / outline for `### steps`
- Completion for metadata names, `kind` values, and `{{ stepName.response.* }}` references
- Diagnostics: missing/duplicate `@name`, unknown `@kind`, invalid `TimeSpan`, unresolved references
- Go-to-definition from `{{ stepName.* }}` to `# @name stepName`
- CodeLens to run a chain or a single step via `Mcf.Cli`
