# Jyro CLI

Jyro CLI is the command-line interface for the Jyro scripting language. It is used to compile, execute, validate, and test Jyro scripts from the terminal. Jyro is an imperative data manipulation language designed for secure, sandboxed processing of JSON-like data structures.

The tool is distributed for Windows, Linux, and macOS.

## Installation

The .NET 10.0 runtime is required. Pre-built binaries are published for each supported platform. Once downloaded, the executable should be placed on the system PATH so that it can be invoked from any directory.

On **Windows**, the executable is named `jyro.exe`. On **Linux** and **macOS**, it is named `jyro` and must be marked as executable after download:

```bash
chmod +x jyro
```

Installation can be verified by running:

```
jyro --help
```

This will display the root help text along with a list of available subcommands.

## Overview

Jyro CLI provides four subcommands:

| Command    | Alias | Purpose                                                       |
|------------|-------|---------------------------------------------------------------|
| `compile`  | `c`   | Compile a `.jyro` source file to a `.jyrx` precompiled binary |
| `run`      | `r`   | Execute a `.jyro` script or `.jyrx` binary                    |
| `validate` | `v`   | Verify that a `.jyrx` binary matches a given `.jyro` source   |
| `test`     | `t`   | Execute a script and compare its output to expected JSON       |

Each subcommand accepts its own set of options. The short alias may be used interchangeably with the full command name (for example, `jyro c` is equivalent to `jyro compile`).

## Script File Formats

Two script file formats are supported:

- **`.jyro`** -- Plain-text UTF-8 source files containing Jyro script code.
- **`.jyrx`** -- Precompiled binary files produced by the `compile` command. A `.jyrx` file embeds a SHA-256 hash of the original source, which is used by the `validate` command for integrity verification.

All data input and output files are standard JSON.

---

## `compile`

```
jyro compile -i <script.jyro> [-o <output.jyrx>] [plugin options] [logging options]
```

The `compile` command is used to transform a `.jyro` source file into a `.jyrx` precompiled binary. Precompilation allows the parsing, validation, and linking stages of the pipeline to be performed ahead of time so that subsequent execution is faster.

The built-in HTTP functions (`InvokeRestMethod`, `UrlEncode`, `UrlDecode`, `FormEncode`) are always available during compilation. If a script references functions provided by external plugin assemblies, those plugins must be supplied at compile time using the plugin options described below (see [Plugin Loading](#plugin-loading) for details).

### Options

| Option                 | Alias | Required | Description                                                                  |
|------------------------|-------|----------|------------------------------------------------------------------------------|
| `--input-script-file`  | `-i`  | Yes      | Path to the `.jyro` source file to be compiled.                              |
| `--output-file`        | `-o`  | No       | Path for the output `.jyrx` file. Defaults to the input filename with a `.jyrx` extension. |
| `--plugin`             | `-p`  | No       | Comma-separated paths to individual plugin assembly DLL files.               |
| `--plugin-directory`   | `-pd` | No       | Comma-separated directories to search for plugin DLLs (top-level only).      |
| `--plugin-recursive`   | `-pr` | No       | Comma-separated directories to search recursively for plugin DLLs.           |
| `--log-file`           | `-l`  | No       | Path to a file for log output. Logging is written to the console by default. |
| `--verbosity`          | `-v`  | No       | Minimum log level. Accepted values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Defaults to `Information`. |
| `--quiet`              | `-q`  | No       | Disables all logging output.                                                 |
| `--console-logging`    |       | No       | Enables or disables console logging (`true` or `false`). Defaults to `true`. |
| `--log-format`         |       | No       | Log output format (`text` or `json`). Defaults to `text`.                    |

### Usage

A script is compiled as follows:

```bash
jyro compile -i transform.jyro
```

This will produce `transform.jyrx` in the same directory. An explicit output path may be specified:

```bash
jyro compile -i transform.jyro -o build/transform.jyrx
```

If the script uses functions from an external plugin, the plugin must be provided:

```bash
jyro compile -i transform.jyro -p MyPlugin.dll
jyro compile -i transform.jyro -pr C:\Plugins
```

On success, a confirmation message is printed indicating the input file, output file, and the size of the compiled binary in bytes:

```
Compiled transform.jyro -> build/transform.jyrx (1284 bytes)
```

If compilation fails, one or more diagnostic messages are printed. Each diagnostic includes an error code, source location (line and column), and a description of the problem:

```
[E0012] Line 5, Column 10: Undefined variable 'x'
```

### Exit Codes

- `0` -- Compilation succeeded.
- `1` -- The input file was not found, or one or more compilation errors were encountered.

---

## `run`

```
jyro run -i <script.jyro|jyrx> [-d <data.json>] [-o <output.json>] [options]
```

The `run` command executes a Jyro script or precompiled binary. The tool automatically detects the file type based on its extension: `.jyrx` files are loaded as precompiled binaries, and all other files are treated as source code.

### Options

| Option                 | Alias | Required | Description                                                                                  |
|------------------------|-------|----------|----------------------------------------------------------------------------------------------|
| `--input-script-file`  | `-i`  | Yes      | Path to the `.jyro` source file or `.jyrx` binary to execute.                                |
| `--data-json-file`     | `-d`  | No       | Path to a JSON file whose contents are made available to the script as the `Data` object. Defaults to an empty object (`{}`). |
| `--output-json-file`   | `-o`  | No       | Path to write the JSON output. If omitted, output is written to stdout.                      |
| `--log-file`           | `-l`  | No       | Path to a file for log output. Logging is written to the console by default.                 |
| `--verbosity`          | `-v`  | No       | Minimum log level. Accepted values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Defaults to `Information`. |
| `--quiet`              | `-q`  | No       | Disables all logging output.                                                                 |
| `--console-logging`    |       | No       | Enables or disables console logging (`true` or `false`). Defaults to `true`.                 |
| `--config`             | `-c`  | No       | Path to a JSON configuration file. If not specified, default locations are searched (see [Configuration](#configuration)). |
| `--stats`              | `-s`  | No       | Displays per-stage pipeline timing statistics on stderr after execution (see [Pipeline Statistics](#pipeline-statistics)). |
| `--log-format`         |       | No       | Log output format (`text` or `json`). Defaults to `text`.                                    |
| `--plugin`             | `-p`  | No       | Comma-separated paths to individual plugin assembly DLL files.                               |
| `--plugin-directory`   | `-pd` | No       | Comma-separated directories to search for plugin DLLs (top-level only).                      |
| `--plugin-recursive`   | `-pr` | No       | Comma-separated directories to search recursively for plugin DLLs.                           |

### Usage

The simplest invocation executes a script with no input data and prints the result to stdout:

```bash
jyro run -i transform.jyro
```

Input data can be supplied from a JSON file. The contents of this file are made available inside the script through the built-in `Data` object:

```bash
jyro run -i transform.jyro -d input.json
```

To write the output to a file rather than stdout:

```bash
jyro run -i transform.jyro -d input.json -o result.json
```

A precompiled binary can be executed in exactly the same way:

```bash
jyro run -i transform.jyrx -d input.json -o result.json
```

Logging verbosity can be adjusted or suppressed entirely:

```bash
jyro run -i transform.jyro -v Debug -l run.log
jyro run -i transform.jyro -q
```

If the script uses functions from external plugins, the plugin assemblies or directories must be provided:

```bash
jyro run -i transform.jyro -p MyPlugin.dll
jyro run -i transform.jyro -pr C:\Plugins,D:\MorePlugins
```

All output JSON is pretty-printed with two-space indentation.

### Exit Codes

- `0` -- Execution succeeded and output was produced.
- `1` -- A required option was missing, a file was not found, or script execution failed.

---

## `validate`

```
jyro validate --compiled <file.jyrx> --raw <file.jyro> [logging options]
```

The `validate` command is used to verify the integrity of a `.jyrx` precompiled binary against its original `.jyro` source file. During compilation, a SHA-256 hash of the source code is embedded in the `.jyrx` binary header. The `validate` command recomputes this hash from the supplied source file and compares it to the stored value.

This is useful in deployment pipelines and review workflows where it must be confirmed that a binary was produced from a specific, unmodified source file.

### Options

| Option              | Alias | Required | Description                                  |
|---------------------|-------|----------|----------------------------------------------|
| `--compiled`        |       | Yes      | Path to the `.jyrx` precompiled binary file. |
| `--raw`             |       | Yes      | Path to the original `.jyro` source file.    |
| `--log-file`        | `-l`  | No       | Path to a file for log output. Logging is written to the console by default. |
| `--verbosity`       | `-v`  | No       | Minimum log level. Accepted values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Defaults to `Information`. |
| `--quiet`           | `-q`  | No       | Disables all logging output.                 |
| `--console-logging` |       | No       | Enables or disables console logging (`true` or `false`). Defaults to `true`. |
| `--log-format`      |       | No       | Log output format (`text` or `json`). Defaults to `text`. |

### Usage

```bash
jyro validate --compiled transform.jyrx --raw transform.jyro
```

If the hashes match, the following is printed in green:

```
[PASS]: transform.jyrx is a valid representation of transform.jyro
```

If the hashes do not match, a failure message is printed in red along with both hash values for diagnostic purposes:

```
[FAIL]: transform.jyrx does NOT match transform.jyro
  Source hash:   A1B2C3...
  Compiled hash: D4E5F6...
```

### Exit Codes

- `0` -- The binary is a valid compilation of the source file.
- `1` -- A file was not found, the binary header could not be read, or the hashes did not match.

---

## `test`

```
jyro test -i <script.jyro|jyrx> [-d <data.json>] -o <expected.json> [-s] [plugin options] [logging options]
```

The `test` command executes a Jyro script and compares its output against an expected JSON file. It is intended for use in automated testing and continuous integration pipelines.

### Options

| Option                 | Alias | Required | Description                                                                                  |
|------------------------|-------|----------|----------------------------------------------------------------------------------------------|
| `--input-script-file`  | `-i`  | Yes      | Path to the `.jyro` source file or `.jyrx` binary to execute.                                |
| `--data-json-file`     | `-d`  | No       | Path to a JSON file providing the script's `Data` object. Defaults to an empty object (`{}`). |
| `--output-json-file`   | `-o`  | Yes      | Path to the expected output JSON file to compare against.                                    |
| `--stats`              | `-s`  | No       | Displays per-stage pipeline timing statistics on stderr.                                     |
| `--plugin`             | `-p`  | No       | Comma-separated paths to individual plugin assembly DLL files.                               |
| `--plugin-directory`   | `-pd` | No       | Comma-separated directories to search for plugin DLLs (top-level only).                      |
| `--plugin-recursive`   | `-pr` | No       | Comma-separated directories to search recursively for plugin DLLs.                           |
| `--log-file`           | `-l`  | No       | Path to a file for log output. Logging is written to the console by default.                 |
| `--verbosity`          | `-v`  | No       | Minimum log level. Accepted values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Defaults to `Information`. |
| `--quiet`              | `-q`  | No       | Disables all logging output.                                                                 |
| `--console-logging`    |       | No       | Enables or disables console logging (`true` or `false`). Defaults to `true`.                 |
| `--log-format`         |       | No       | Log output format (`text` or `json`). Defaults to `text`.                                    |

### Usage

```bash
jyro test -i transform.jyro -d input.json -o expected-output.json
```

If the actual output matches the expected output, a pass message is printed in green:

```
[PASS]: transform.jyro correctly produced output identical to expected-output.json
```

If differences are found, each mismatch is listed with its JSON path, expected value, and actual value:

```
[FAIL]: transform.jyro produced output that differs from expected-output.json
  3 difference(s) found:
    $.name: expected Alice, got Bob
    $.scores[2]: expected 95, got 87
    $.metadata.version: expected 2.0, got (missing)
```

### Comparison Semantics

The comparison is performed semantically rather than as a textual diff. Object property order is not significant. Numeric values are compared with tolerance for floating-point representation differences: a relative tolerance of 1e-10 and an absolute floor of 1e-15 are applied, so values such as `0.1 + 0.2` and `0.3` are treated as equal. Integer and floating-point representations of the same value (e.g., `42` and `42.0`) are also treated as equal.

Array elements are compared positionally. If the arrays differ in length, a mismatch on the `.length` path is reported in addition to any element-level differences.

### Exit Codes

- `0` -- The script executed successfully and the output matched the expected file.
- `1` -- A file was not found, execution failed, or the output differed from the expected file.

### Use in CI

Because the `test` command returns a non-zero exit code on failure, it integrates naturally with CI systems. A typical test script might look like:

```bash
jyro test -i tests/transform.jyro -d tests/input.json -o tests/expected.json
jyro test -i tests/aggregate.jyro -d tests/data.json -o tests/aggregate-expected.json
```

Each line will halt a CI pipeline (under `set -e` or equivalent) if the test fails.

---

## Configuration

The `run` command supports a layered configuration system. Options may be specified through three sources, listed here from lowest to highest priority:

1. **Configuration file** -- A JSON file named `jyro.config.json`.
2. **Environment variables** -- Prefixed with `JYRO_`.
3. **Command-line arguments** -- Passed directly on the command line.

When the same option is set in multiple sources, the highest-priority source wins. For example, a `--verbosity` flag on the command line will override a `LogLevel` value in the configuration file.

### Configuration File

If no `--config` path is given explicitly, the following locations are searched in order:

1. The current working directory: `./jyro.config.json`
2. The application data directory: see platform-specific paths below.
3. The user home directory: `~/jyro.config.json`

The first file found is used. If no file is found in any location, default values are applied.

A configuration file uses the following structure:

```json
{
  "InputScriptFile": "transform.jyro",
  "DataJsonFile": "data.json",
  "OutputJsonFile": "output.json",
  "LogFile": "jyro.log",
  "LogLevel": "Information",
  "ConsoleLogging": true,
  "LogFormat": "text",
  "NoLogging": false,
  "Stats": false,
  "PluginAssemblies": null,
  "PluginDirectories": null,
  "PluginRecursiveDirectories": null
}
```

All fields are optional. Only the fields that differ from the defaults need to be included.

#### Platform-Specific Application Data Paths

The application data directory varies by platform:

| Platform | Path                                              |
|----------|---------------------------------------------------|
| Windows  | `%APPDATA%\Jyro\jyro.config.json`                |
| Linux    | `$XDG_CONFIG_HOME/Jyro/jyro.config.json` (typically `~/.config/Jyro/jyro.config.json`) |
| macOS    | `~/Library/Application Support/Jyro/jyro.config.json` |

These paths are resolved through the .NET `Environment.SpecialFolder.ApplicationData` API, which maps to the appropriate location on each operating system.

### Environment Variables

Environment variables are prefixed with `JYRO_` and use the same key names as the configuration file. For example:

```bash
export JYRO_InputScriptFile=transform.jyro
export JYRO_DataJsonFile=data.json
export JYRO_LogLevel=Debug
export JYRO_LogFormat=json
export JYRO_PluginRecursiveDirectories=/opt/jyro-plugins
```

On Windows, the equivalent is:

```cmd
set JYRO_InputScriptFile=transform.jyro
set JYRO_DataJsonFile=data.json
set JYRO_LogLevel=Debug
set JYRO_LogFormat=json
set JYRO_PluginRecursiveDirectories=C:\Plugins
```

---

## Logging

All subcommands support structured logging. Log messages are formatted with a timestamp, level, and category:

```
2026-02-14 10:30:00 [Information] Jyro.Cli.Compile: Compiling transform.jyro
```

### Options

| Option              | Alias | Default         | Effect                                                                 |
|---------------------|-------|-----------------|------------------------------------------------------------------------|
| `--verbosity`       | `-v`  | `Information`   | Sets the minimum log level. Messages below this level are discarded.   |
| `--quiet`           | `-q`  | `false`         | Disables all logging output (console and file). Overrides other options.|
| `--console-logging` |       | `true`          | Enables or disables the console log provider.                          |
| `--log-file`        | `-l`  |                 | Appends log output to the specified file. The directory is created automatically if it does not exist. |
| `--log-format`      |       | `text`          | Selects the log output format: `text` for human-readable lines, `json` for structured JSON. |

The accepted `--verbosity` values, from most to least verbose, are: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`.

### Logging behavior by subcommand

| Subcommand  | Console log destination | Script `Log()` supported | Config file / env vars |
|-------------|-------------------------|--------------------------|------------------------|
| `compile`   | stdout                  | No (scripts are not executed) | No              |
| `run`       | stdout                  | Yes                      | Yes                    |
| `validate`  | stdout                  | No (scripts are not executed) | No              |
| `test`      | stderr                  | Yes                      | No                     |

The `test` command writes console log output to stderr so that it does not interfere with the PASS/FAIL results on stdout. This means log messages can be redirected independently:

```bash
jyro test -i script.jyro -o expected.json 2>test.log
```

The `run` command's logging options can also be set via configuration file and environment variables (see [Configuration](#configuration)). The other subcommands accept logging options from the command line only.

### Output routing summary

The following table shows where log output ends up based on the options provided.

| `--quiet` | `--console-logging` | `--log-file` | Console output | File output |
|-----------|---------------------|--------------|----------------|-------------|
|           |                     |              | Yes            | No          |
|           |                     | `log.txt`    | Yes            | Yes         |
|           | `false`             |              | No             | No          |
|           | `false`             | `log.txt`    | No             | Yes         |
| `-q`      |                     |              | No             | No          |
| `-q`      |                     | `log.txt`    | No             | No          |

When `--quiet` is set, all logging is suppressed regardless of other options. When `--console-logging false` is set, only file logging remains active (if a log file is specified).

### Log formats

The default `text` format produces human-readable single-line output:

```
2026-02-14 10:30:00 [Information] Jyro.Cli.Compile: Compiling transform.jyro
```

The `json` format produces one JSON object per line ([JSON Lines](https://jsonlines.org/) format), suitable for machine parsing and ingestion by Jyro scripts or CI/CD tooling:

```json
{"Timestamp":"2026-02-14T10:30:00.123+00:00","EventId":0,"LogLevel":"Information","Category":"Jyro.Cli.Compile","Message":"Compiling transform.jyro"}
```

Both console and file output respect the chosen format. The JSON format applies to both providers identically, so `--log-format json -l build.log` produces JSON Lines in the log file as well.

### Logging from Scripts

The `run` and `test` commands register a `Log` host function that lets Jyro scripts write to the logging pipeline. It accepts a level string and a message:

```
Log("info", "Processing started")
Log("debug", "Record count: 42")
Log("error", "Unexpected null in field 'name'")
```

The accepted level values are: `trace`, `debug`, `info` (or `information`), `warn` (or `warning`), `error`, and `critical`. An invalid level will cause a runtime error. Messages from scripts are prefixed with `[Script]` in the log output to distinguish them from runtime messages.

The `compile` and `validate` commands do not execute scripts and therefore do not support the `Log` function.

---

## Pipeline Statistics

When the `--stats` (or `-s`) flag is passed to the `run` or `test` commands, per-stage timing information is printed to stderr after execution completes. This is useful for identifying performance bottlenecks.

For a `.jyro` source file, the full pipeline is displayed:

```
Pipeline Statistics:
  ----------------------------
  Parse:           1.23 ms
  Validate:        0.45 ms
  Link:            0.12 ms
  Compile:         0.89 ms
  Execute:         2.34 ms
  ----------------------------
  Total:           5.03 ms
```

For a `.jyrx` precompiled binary, only the stages that apply are shown:

```
Pipeline Statistics:
  ----------------------------
  Deserialize:     0.31 ms
  Compile:         0.67 ms
  Execute:         2.18 ms
  ----------------------------
  Total:           3.16 ms
```

Statistics are written to stderr so that they do not interfere with JSON output on stdout. This means they can be viewed even when output is piped to another command or redirected to a file.

---

## Plugin Loading

Jyro CLI supports loading custom functions from external .NET assemblies at both compile time and run time. This allows third-party or in-house libraries to extend the Jyro language with additional functions beyond the built-in standard library and HTTP functions.

All function references in a Jyro script must be resolvable at compile time. If a script calls a function that is not part of the standard library or the built-in HTTP functions, the plugin assembly containing that function must be provided via the plugin options. This applies to both the `compile` and `run` commands.

### Plugin Options

Three options are available on the `compile`, `run`, and `test` commands. All accept comma-separated values for specifying multiple paths.

| Option               | Alias | Description                                                    |
|----------------------|-------|----------------------------------------------------------------|
| `--plugin`           | `-p`  | Paths to individual plugin assembly DLL files.                 |
| `--plugin-directory`  | `-pd` | Directories to search for `*.dll` plugin files (top-level only). |
| `--plugin-recursive` | `-pr` | Directories to search recursively for `*.dll` plugin files.    |

### Examples

Load a single plugin assembly:

```bash
jyro compile -i script.jyro -p MyPlugin.dll
jyro run -i script.jyro -p MyPlugin.dll
```

Load multiple plugin assemblies:

```bash
jyro run -i script.jyro -p Plugin1.dll,Plugin2.dll
```

Search a directory for plugins:

```bash
jyro compile -i script.jyro -pd C:\Plugins
```

Recursively search multiple directories:

```bash
jyro run -i script.jyro -pr C:\Plugins,D:\MorePlugins
```

Options can be combined freely:

```bash
jyro run -i script.jyro -p SpecialPlugin.dll -pr C:\Plugins
```

### Configuration File

Plugin paths can also be specified in `jyro.config.json`:

```json
{
  "PluginAssemblies": "Plugin1.dll,Plugin2.dll",
  "PluginDirectories": "C:\\Plugins",
  "PluginRecursiveDirectories": "C:\\Plugins\\Extensions"
}
```

Command-line arguments override configuration file values, following the standard [configuration precedence](#configuration).

### Creating Plugins

To create a plugin, build a .NET class library that references the `Mesch.Jyro` package and implement one or more functions by inheriting from `JyroFunctionBase`:

```csharp
using Mesch.Jyro;

public class MyCustomFunction : JyroFunctionBase
{
    public MyCustomFunction()
        : base("MyFunction",
               FunctionSignatures.unary("MyFunction",
                   ParameterType.StringParam, ParameterType.StringParam))
    {
    }

    public override JyroValue Execute(
        IReadOnlyList<JyroValue> arguments,
        JyroExecutionContext context)
    {
        var input = arguments[0].AsString();
        return JyroValue.FromObject($"Processed: {input}");
    }
}
```

Compile the class library to a DLL and provide it to Jyro CLI using the plugin options.

**Requirements:**

- Plugin functions must have public parameterless constructors.
- Plugin assemblies must reference the `Mesch.Jyro` package.
- All function types must implement `IJyroFunction` (typically by inheriting from `JyroFunctionBase`).

### Discovery Behavior

When loading from a directory (`-pd` or `-pr`), the CLI scans all `*.dll` files and discovers any concrete, non-abstract classes that implement `IJyroFunction`. The following are silently skipped:

- Non-.NET assemblies (native DLLs).
- Assemblies that cannot be loaded due to missing dependencies.
- Types that cannot be instantiated (for example, those without a parameterless constructor).

When loading an individual assembly (`-p`), instantiation errors are reported immediately as failures.

---

## Exit Codes

All subcommands follow a consistent convention:

| Code | Meaning                                                                 |
|------|-------------------------------------------------------------------------|
| `0`  | The operation completed successfully.                                   |
| `1`  | An error occurred: a file was not found, compilation or execution failed, or a comparison did not match. |

Error messages are printed in red to the console. Diagnostic messages from the compiler include an error code, the source location (line and column), and a human-readable description.

---

## Platform Notes

### Windows

The executable is named `jyro.exe`. Path separators may use either forward slashes or backslashes. The application data directory for configuration files is `%APPDATA%\Jyro\`.

### Linux

The executable is named `jyro` and must be marked as executable with `chmod +x jyro` after download. The application data directory is typically `~/.config/Jyro/`, governed by `$XDG_CONFIG_HOME`.

### macOS

The executable is named `jyro` and must be marked as executable with `chmod +x jyro` after download. On recent versions of macOS, Gatekeeper may block unsigned binaries downloaded from the internet. If this occurs, the quarantine attribute can be removed:

```bash
xattr -d com.apple.quarantine jyro
```

The application data directory is `~/Library/Application Support/Jyro/`.

---

## License

Jyro CLI is released under the MIT License. See [LICENSE](LICENSE) for details.
