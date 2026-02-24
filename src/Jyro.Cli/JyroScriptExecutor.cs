using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mesch.Jyro;
using Mesch.Jyro.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Core;

namespace Jyro.Cli;

/// <summary>
/// Executes Jyro scripts based on command-line options.
/// </summary>
internal sealed class JyroScriptExecutor : IJyroScriptExecutor
{
    private readonly ILogger<JyroScriptExecutor> _logger;
    private readonly JyroCommandOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JyroScriptExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The command options.</param>
    public JyroScriptExecutor(
        ILogger<JyroScriptExecutor> logger,
        IOptions<JyroCommandOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Executes the Jyro script according to the configured options.
    /// </summary>
    /// <returns>A task containing the exit code: 0 for success, non-zero for failure.</returns>
    public async Task<int> ExecuteAsync()
    {
        _options.Validate();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Starting Jyro execution. Script={Script}, Data={Data}, Output={Output}",
                _options.InputScriptFile,
                _options.DataJsonFile ?? "(empty object)",
                _options.OutputJsonFile ?? "(stdout)");
        }

        // Validate the input script file
        if (!File.Exists(_options.InputScriptFile))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Input script file not found: {_options.InputScriptFile}");
            Console.ResetColor();
            return 1;
        }

        var data = await LoadDataAsync();

        // Detect .jyrx precompiled binary vs .jyro source
        if (Path.GetExtension(_options.InputScriptFile).Equals(".jyrx", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(_options.InputScriptFile);
            return await ExecuteCompiledAsync(bytes, data);
        }

        var script = await File.ReadAllTextAsync(_options.InputScriptFile);
        return await ExecuteScriptAsync(script, data);
    }

    /// <summary>
    /// Loads the JSON data file or returns an empty object if no file is specified.
    /// </summary>
    /// <returns>A JyroValue representing the loaded data.</returns>
    private async Task<JyroValue> LoadDataAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.DataJsonFile))
        {
            return JyroValue.FromJson("{}", FSharpOption<JsonSerializerOptions>.None);
        }

        var dataJson = await File.ReadAllTextAsync(_options.DataJsonFile);
        return JyroValue.FromJson(dataJson, FSharpOption<JsonSerializerOptions>.None);
    }

    /// <summary>
    /// Executes a precompiled .jyrx binary.
    /// </summary>
    /// <param name="compiledBytes">The .jyrx binary data.</param>
    /// <param name="data">The input data.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    private async Task<int> ExecuteCompiledAsync(byte[] compiledBytes, JyroValue data)
    {
        _logger.LogInformation("Executing binary");

        var builder = new JyroBuilder()
            .WithCompiledBytes(compiledBytes)
            .WithData(data)
            .UseStdlib()
            .UseHttpFunctions()
            .AddFunction(new LogFunction(_logger));

        LoadPlugins(builder);

        JyroPipelineStats? stats = null;
        if (_options.Stats)
        {
            stats = new JyroPipelineStats();
            builder = builder.WithStats(stats);
        }

        var result = builder.Execute();
        var exitCode = await OutputExecutionResultsAsync(result);

        if (stats != null)
        {
            JyroCommandFactory.PrintPipelineStats(stats);
        }

        return exitCode;
    }

    /// <summary>
    /// Executes the Jyro script.
    /// </summary>
    /// <param name="script">The script source code.</param>
    /// <param name="data">The input data.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    private async Task<int> ExecuteScriptAsync(string script, JyroValue data)
    {
        _logger.LogInformation("Executing script");

        var builder = new JyroBuilder()
            .WithSource(script)
            .WithData(data)
            .UseStdlib()
            .UseHttpFunctions()
            .AddFunction(new LogFunction(_logger));

        LoadPlugins(builder);

        JyroPipelineStats? stats = null;
        if (_options.Stats)
        {
            stats = new JyroPipelineStats();
            builder = builder.WithStats(stats);
        }

        var result = builder.Execute();
        var exitCode = await OutputExecutionResultsAsync(result);

        if (stats != null)
        {
            JyroCommandFactory.PrintPipelineStats(stats);
        }

        return exitCode;
    }

    /// <summary>
    /// Loads plugin functions from assemblies and directories specified in options.
    /// </summary>
    private void LoadPlugins(JyroBuilder builder)
    {
        var functions = PluginLoader.LoadAll(
            _options.PluginAssemblies,
            _options.PluginDirectories,
            _options.PluginRecursiveDirectories);

        foreach (var fn in functions)
        {
            builder.AddFunction(fn);
        }
    }

    /// <summary>
    /// Outputs the script execution results to console and/or file.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    private async Task<int> OutputExecutionResultsAsync(JyroResult<JyroValue> result)
    {
        if (!result.IsSuccess)
        {
            foreach (var msg in result.Messages)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(FormatMessage(msg));
                Console.ResetColor();
            }
            return 1;
        }

        // Get the value from F# Option
        var resultValue = FSharpOption<JyroValue>.get_IsSome(result.Value)
            ? result.Value.Value
            : null;

        if (resultValue == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Execution returned null result.");
            Console.ResetColor();
            return 1;
        }

        var outputJson = SerializeJyroValue(resultValue.ToObjectValue());

        if (!string.IsNullOrWhiteSpace(_options.OutputJsonFile))
        {
            await File.WriteAllTextAsync(_options.OutputJsonFile, outputJson);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Execution complete. Output written to {OutputFile}", _options.OutputJsonFile);
            }
        }
        else
        {
            Console.WriteLine(outputJson);
            _logger.LogInformation("Execution complete. Output written to stdout");
        }

        return 0;
    }

    /// <summary>
    /// Formats a diagnostic message for display.
    /// </summary>
    private static string FormatMessage(DiagnosticMessage msg) =>
        DiagnosticFormatter.formatMessage(msg);
    /// <summary>
    /// Serializes a JyroValue object graph to indented JSON without using JsonSerializer.Serialize(object),
    /// which requires reflection and is not trim-safe.
    /// </summary>
    internal static string SerializeJyroValue(object? value)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        WriteValue(writer, value);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                WriteDouble(writer, d);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case List<object> list:
                writer.WriteStartArray();
                foreach (var item in list)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case Dictionary<string, object> dict:
                writer.WriteStartObject();
                foreach (var kvp in dict)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteValue(writer, kvp.Value);
                }
                writer.WriteEndObject();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    private static void WriteDouble(Utf8JsonWriter writer, double value)
    {
        if (double.IsFinite(value) && value == Math.Floor(value) && Math.Abs(value) < 1e15)
        {
            // Write whole-number doubles with .0 to preserve float type in JSON
            writer.WriteRawValue($"{value:F1}");
        }
        else
        {
            // Round to 10 decimal places to eliminate IEEE 754 precision artifacts
            // e.g. 80.49000000000001 → 80.49, 39.989999999999995 → 39.99
            writer.WriteNumberValue(Math.Round(value, 10));
        }
    }
}
