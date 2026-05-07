# message-chain
Chain HTTP and CEP requests in a single plain-text file — a .http-inspired, Liquid-templated, VCS-friendly format with cross-step references and conditional execution.

The repo ships two pieces:

- **MCF** — the file format. See [MCF.md](MCF.md).
- **`msgchain`** — the [.NET 10](https://dotnet.microsoft.com/download) CLI that parses, renders and runs an MCF document.

## CLI — `msgchain`

`msgchain` is a single-binary CLI built from [src/Mcf.Cli](src/Mcf.Cli/Mcf.Cli.csproj). It takes an MCF document, executes its steps top-to-bottom against the configured HTTP / CEP endpoints, and reports a live listr2-style progress block.

### Commands

#### `msgchain run`

Execute an MCF document.

| Option | Description |
| --- | --- |
| `-f`, `--file <path>` | Path to an `.mcf` file. Mutually exclusive with `--raw`. |
| `-r`, `--raw <text>` | Inline MCF chain text. Mutually exclusive with `--file`. |
| `-e`, `--env <KEY=VALUE>` | Set a variable. Repeatable; later values override earlier ones. |
| `--env-file <path>` | Load variables from a dotenv-style file. Repeatable; later files override earlier ones. Lower precedence than `--env`. |
| `--log <path>` | Write a per-step wire log (raw request/response pairs) to a file. |
| `--report <path>` | Write a run summary table to a file. |

Exit codes: `0` success, `1` at least one step failed (or a fatal error during run), `2` invalid input (could not read chain or env files), `130` canceled (Ctrl+C).

### Examples

Run a chain from a file with two variables:

```powershell
msgchain run -f .\login.mcf -e BASE_URL=https://api.example.com -e USER=alice
```

Layer a dotenv file under inline overrides, and persist both artefacts:

```powershell
msgchain run -f .\flow.mcf --env-file .\.env --env TOKEN=devtoken `
    --log .\out\flow.log --report .\out\flow.report
```

Pipe a chain in via `--raw`:

```powershell
msgchain run --raw "### ping`nGET {{ BASE_URL }}/health" -e BASE_URL=https://api.example.com
```

## TODOs

