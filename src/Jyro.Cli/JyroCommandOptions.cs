using Microsoft.Extensions.Logging;

namespace Jyro.Cli;

/// <summary>
/// Represents the command-line options for the Jyro console application.
/// </summary>
public sealed class JyroCommandOptions
{
    /// <summary>
    /// Gets or sets the path to the Jyro script file to execute.
    /// </summary>
    public string? InputScriptFile { get; set; }

    /// <summary>
    /// Gets or sets the path to the JSON data file that will be provided as the script's Data object.
    /// </summary>
    public string? DataJsonFile { get; set; }

    /// <summary>
    /// Gets or sets the path for the JSON output file. If null, output goes to stdout.
    /// </summary>
    public string? OutputJsonFile { get; set; }

    /// <summary>
    /// Gets or sets the path for logging output. If null, logs go to console only.
    /// </summary>
    public string? LogFile { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets a value indicating whether logging is completely disabled.
    /// </summary>
    public bool NoLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether console logging is enabled.
    /// </summary>
    public bool ConsoleLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the log output format. Accepted values: "text" (default) or "json".
    /// </summary>
    public string LogFormat { get; set; } = "text";

    /// <summary>
    /// Gets or sets a value indicating whether to collect and display pipeline stage timing statistics.
    /// </summary>
    public bool Stats { get; set; }

    /// <summary>
    /// Gets or sets comma-separated paths to individual plugin assembly DLL files.
    /// </summary>
    public string? PluginAssemblies { get; set; }

    /// <summary>
    /// Gets or sets comma-separated directory paths to search for plugin DLLs (top-level only).
    /// </summary>
    public string? PluginDirectories { get; set; }

    /// <summary>
    /// Gets or sets comma-separated directory paths to search recursively for plugin DLLs.
    /// </summary>
    public string? PluginRecursiveDirectories { get; set; }

    /// <summary>
    /// Validates the options and applies smart defaults.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required options are missing or invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputScriptFile))
        {
            throw new InvalidOperationException("Input script file is required.");
        }

        if (!File.Exists(InputScriptFile))
        {
            throw new InvalidOperationException($"Input script file not found: {InputScriptFile}");
        }

        if (!string.IsNullOrWhiteSpace(DataJsonFile) && !File.Exists(DataJsonFile))
        {
            throw new InvalidOperationException($"Data JSON file not found: {DataJsonFile}");
        }
    }
}
