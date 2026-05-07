# CEP — Command Execution Protocol

CEP (Command Execution Protocol) defines a simple text-based message format for describing and executing command-line invocations, loosely inspired by HTTP message structure.

## Protocol Version

Current version: **CEP/0.1**

File extension: `.cep`

---

## Document Structure

A CEP document is a single message — either a **request** or a **response**. A message is made up of three kinds of lines:

1. **Comments** — lines whose first non-whitespace character is `#`. Ignored by the parser. Defined for request messages only; response messages do not recognize comments.
2. **Structural lines** — the start-line (request) or status-line (response), zero or more header lines, and the blank line that terminates the headers section.
3. **Payload lines** — argument lines (request) or opaque output lines (response).

```
<start-line or status-line>
<Header-Name>: <Header-Value>
...

<Payload>
```

### Encoding and Line Endings

- Files are UTF-8, with or without BOM.
- Both `CRLF` and `LF` line endings are accepted.
- The `Charset` request header applies only to the **child process** standard output/error encoding; it does not affect the encoding of the CEP message itself.

### Comments

Comments may appear anywhere in a **request** message — before the start-line, within the headers section, or within the arguments section — and do not count as the blank line that terminates the headers section.

```
# run ffmpeg to remux
EXEC ffmpeg CEP/0.1
# use the user's Downloads folder
Working-Directory: ${USERPROFILE}\Downloads

-i video.mp4
# force overwrite
-y
output.mp4
```

### Placeholder Expansion

Request header values and argument values support `${VAR_NAME}` placeholders:

- Variable names must match `^[A-Za-z_][A-Za-z0-9_]*$`.
- Placeholders are resolved **only** against the **host process** environment variables (the environment of the CEP executor). No other source is consulted.
- Unknown variables and syntactically invalid placeholders are kept as-is.

Placeholder expansion is **not** applied to:

- The request start-line or the response status-line.
- Header **names**.
- Any part of a response message.

### Environment Variable Injection

If a request header name matches `^[A-Za-z_][A-Za-z0-9_]*$` (i.e. a valid environment variable name), the header name and its (already-expanded) value are injected into the **child process** environment.

The child process environment is distinct from the host process environment used during placeholder expansion; injecting a header does not redefine a host variable.

---

## Request Message

A CEP request message consists of three sections. The headers and arguments sections are separated by a blank line:

```
<Verb> <Command> <Protocol>
<Header-Name>: <Header-Value>
...

<Arguments>
```

### Start-Line

```
EXEC <command> CEP/0.1
```

| Field      | Description                                          |
|------------|------------------------------------------------------|
| `Verb`     | Action to perform. Currently only `EXEC` is defined. |
| `Command`  | Executable command name or path.                     |
| `Protocol` | Protocol token and version, e.g. `CEP/0.1`.          |

### Headers

Zero or more `Name: Value` lines, terminated by a blank line. Header names are **case-insensitive**. Header values are subject to [placeholder expansion](#placeholder-expansion); headers whose names are valid identifiers are additionally subject to [environment variable injection](#environment-variable-injection).

| Header              | Description                                                               | Example                    |
|---------------------|---------------------------------------------------------------------------|----------------------------|
| `Working-Directory` | Working directory for process execution.                                  | `${USERPROFILE}\Downloads` |
| `Timeout`           | Timeout in **seconds**. Values less than or equal to `0` mean no timeout. | `30`                       |
| `Charset`           | Encoding name for the child process's stdout/stderr.                      | `GBK`                      |

Unknown headers are passed through unchanged and follow the injection rule above when their names qualify.

### Arguments

Each non-blank, non-comment line after the header section represents one argument. Argument values are subject to [placeholder expansion](#placeholder-expansion).

- **Token argument** — A single token on a line, passed as-is to the process argument list.
- **Named argument** — Two parts separated by the first run of whitespace on the line. The first part is the option name and the rest is the option value; they are added as two separate entries in the argument list.

```
-i video.mp4          ← named argument: name="-i", value="video.mp4"
-y                    ← token argument: "-y"
output.mp4            ← token argument: "output.mp4"
```

---

## Response Message

A CEP response message is returned after command execution:

```
<Protocol> <ExitCode> <Reason>
<Header-Name>: <Header-Value>
...

<Payload>
```

Response messages do not perform placeholder expansion and do not recognize comments.

### Status-Line

```
CEP/0.1 <exit-code> <reason>
```

| Field      | Description                                                                 |
|------------|-----------------------------------------------------------------------------|
| `Protocol` | Protocol token, echoed from the request.                                    |
| `ExitCode` | Integer exit code. `0` indicates success; other codes are either propagated from the process or assigned by the protocol for conditions where no process exit code is available. |
| `Reason`   | A short status token summarizing the outcome.                               |

### Exit Codes

| Code     | Meaning  | Description                                                                 |
|----------|----------|-----------------------------------------------------------------------------|
| `0`      | OK       | Process completed successfully. Reason: `OK`.                               |
| non-zero | Unknown  | Process completed with a non-zero exit code propagated from the process itself. The specific code is whatever the process returned. Recommended reason: `Unknown`. |
| `124`    | Timeout  | Process exceeded the configured timeout. Recommended reason: `Timeout`.     |
| `125`    | Error    | CEP executor failed before or around process execution (e.g. command not found, failed to start). Recommended reason: `Error`. |
| `130`    | Canceled | Execution was canceled by the caller. Recommended reason: `Canceled`.       |

Synthetic codes (`124`, `125`, `130`) are assigned by the executor and take precedence over the process's own exit code when the corresponding condition occurs. If a process happens to return one of these codes on its own, it is indistinguishable by code alone; `Reason` is the authoritative discriminator.

### Headers

| Header              | Description                        |
|---------------------|------------------------------------|
| `Working-Directory` | Actual working directory used.     |
| `Process-Id`        | OS process ID.                     |
| `Start-Time`        | Process start time (ISO 8601).     |
| `Exit-Time`         | Process exit time (ISO 8601).      |
| `Total-Time`        | Total processor time consumed.     |
| `User-Time`         | User-mode processor time consumed. |

Headers may be omitted when the corresponding value is unavailable (e.g. `Process-Id` / `User-Time` when the process never started).

`Process-Id` and `User-Time` are reported only for responses that represent a natural process completion (exit code `0` or a process-propagated non-zero code). For executor-synthesized outcomes — `124` (`Timeout`), `125` (`Error`), and `130` (`Canceled`) — these two headers are omitted, even if a process was started. The remaining timing headers (`Working-Directory`, `Start-Time`, `Exit-Time`, `Total-Time`) are still reported on a best-effort basis for those outcomes; any of them — including `Total-Time` — may be omitted when the executor cannot obtain a meaningful value (for example, when the process never started or has already been disposed).

### Payload

The payload carries the textual output captured from the child process. How stdout and stderr are combined is determined by the executor, not by the message format. The reference executor exposes a `MergeStandardOutputAndStandardError` switch:

- **Merged** — payload is the concatenation of stdout and stderr.
- **Separated** — payload is stdout; if stdout is empty, it falls back to stderr.

Trailing whitespace (including trailing blank lines and the line terminator following the last payload line) is not significant and may be trimmed by parsers. Callers that require byte-exact output should not rely on the CEP response message to preserve it verbatim.

---

## Examples

### Minimal request

```
EXEC dotnet CEP/0.1

--version
```

### Request with headers

```
EXEC ffmpeg CEP/0.1
Working-Directory: ${USERPROFILE}\Downloads

-i video.mp4
-i audio.mp4
-c:v copy
-c:a aac
-map 0:v:0
-map 1:a:0
-y
output.mp4
```

### Request with charset

```
EXEC ping CEP/0.1
Charset: GBK

baidu.com
```
