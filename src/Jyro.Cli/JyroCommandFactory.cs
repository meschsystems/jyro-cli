using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mesch.Jyro;
using Mesch.Jyro.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.FSharp.Core;

namespace Jyro.Cli;

/// <summary>
/// Factory for creating the root command with all subcommands configured.
/// </summary>
internal sealed class JyroCommandFactory
{
    /// <summary>
    /// Creates the root command with all subcommands configured.
    /// </summary>
    public RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand(
            "Jyro - an imperative data manipulation language for secure, sandboxed processing of JSON-like data structures.");

        rootCommand.Subcommands.Add(CreateCompileCommand());
        rootCommand.Subcommands.Add(CreateRunCommand());
        rootCommand.Subcommands.Add(CreateValidateCommand());
        rootCommand.Subcommands.Add(CreateTestCommand());

        return rootCommand;
    }

    /// <summary>
    /// Creates the compile subcommand: jyro compile|c -i script.jyro [-o script.jyrx] [plugin opts]
    /// </summary>
    private static Command CreateCompileCommand()
    {
        var command = new Command("compile", "Compile a .jyro script to .jyrx precompiled binary");
        command.Aliases.Add("c");

        var inputOption = new Option<string>("--input-script-file")
        {
            Description = "Path to the Jyro script to compile",
            Required = true
        };
        inputOption.Aliases.Add("-i");

        var outputOption = new Option<string?>("--output-file")
        {
            Description = "Output path for the .jyrx file (defaults to same name with .jyrx extension)"
        };
        outputOption.Aliases.Add("-o");

        var (pluginOption, pluginDirOption, pluginRecursiveOption) = CreatePluginOptions();
        var (logFileOption, verbosityOption, quietOption, consoleLoggingOption, logFormatOption) = CreateLoggingOptions();

        command.Options.Add(inputOption);
        command.Options.Add(outputOption);
        command.Options.Add(pluginOption);
        command.Options.Add(pluginDirOption);
        command.Options.Add(pluginRecursiveOption);
        command.Options.Add(logFileOption);
        command.Options.Add(verbosityOption);
        command.Options.Add(quietOption);
        command.Options.Add(consoleLoggingOption);
        command.Options.Add(logFormatOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var inputFile = parseResult.GetValue(inputOption)!;
            var outputFile = parseResult.GetValue(outputOption);
            var plugins = parseResult.GetValue(pluginOption);
            var pluginDirs = parseResult.GetValue(pluginDirOption);
            var pluginRecursiveDirs = parseResult.GetValue(pluginRecursiveOption);

            using var loggerFactory = CreateLoggerFactory(
                parseResult.GetValue(logFileOption),
                parseResult.GetValue(verbosityOption),
                parseResult.GetValue(quietOption),
                parseResult.GetValue(consoleLoggingOption),
                parseResult.GetValue(logFormatOption));
            var logger = loggerFactory.CreateLogger("Jyro.Cli.Compile");

            if (!File.Exists(inputFile))
            {
                WriteError($"Error: Input script file not found: {inputFile}");
                return 1;
            }

            logger.LogInformation("Compiling {InputFile}", inputFile);

            var source = await File.ReadAllTextAsync(inputFile);
            var builder = new JyroBuilder()
                .WithSource(source)
                .UseStdlib()
                .UseHttpFunctions();

            // Load plugin functions
            try
            {
                var pluginFunctions = PluginLoader.LoadAll(plugins, pluginDirs, pluginRecursiveDirs).ToList();
                foreach (var fn in pluginFunctions)
                {
                    builder.AddFunction(fn);
                }

                if (pluginFunctions.Count > 0)
                {
                    logger.LogInformation("Loaded {Count} plugin function(s)", pluginFunctions.Count);
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error loading plugins: {ex.Message}");
                return 1;
            }

            var result = builder.CompileToBytes();

            if (!result.IsSuccess)
            {
                PrintDiagnostics(result.Messages);
                return 1;
            }

            outputFile ??= Path.ChangeExtension(inputFile, ".jyrx");
            var bytes = result.Value.Value;
            await File.WriteAllBytesAsync(outputFile, bytes);
            Console.WriteLine($"Compiled {inputFile} -> {outputFile} ({bytes.Length} bytes)");
            logger.LogInformation("Compilation complete. Output: {OutputFile} ({Size} bytes)", outputFile, bytes.Length);
            return 0;
        });

        return command;
    }

    /// <summary>
    /// Creates the run subcommand: jyro run|r -i script.jyro|jyrx [-d data.json] [-o output.json] [logging opts]
    /// </summary>
    private static Command CreateRunCommand()
    {
        var command = new Command("run", "Run a .jyro script or .jyrx binary");
        command.Aliases.Add("r");

        var inputOption = new Option<string?>("--input-script-file")
        {
            Description = "Path to the Jyro script or .jyrx binary to execute"
        };
        inputOption.Aliases.Add("-i");

        var dataOption = new Option<string?>("--data-json-file")
        {
            Description = "JSON file providing the script's Data object (defaults to empty object)"
        };
        dataOption.Aliases.Add("-d");

        var outputOption = new Option<string?>("--output-json-file")
        {
            Description = "Output file for script results (defaults to stdout)"
        };
        outputOption.Aliases.Add("-o");

        var logFileOption = new Option<string?>("--log-file")
        {
            Description = "File for logging output (defaults to console only)"
        };
        logFileOption.Aliases.Add("-l");

        var logLevelOption = new Option<LogLevel?>("--verbosity")
        {
            Description = "Minimum log level (Trace, Debug, Information, Warning, Error, Critical, None)"
        };
        logLevelOption.Aliases.Add("-v");

        var noLoggingOption = new Option<bool>("--quiet")
        {
            Description = "Disable all logging output"
        };
        noLoggingOption.Aliases.Add("-q");

        var consoleLoggingOption = new Option<bool?>("--console-logging")
        {
            Description = "Enable console logging output (true/false). Default true."
        };

        var configOption = new Option<string?>("--config")
        {
            Description = "Path to JSON configuration file (searches default locations if not specified)"
        };
        configOption.Aliases.Add("-c");

        var statsOption = new Option<bool>("--stats")
        {
            Description = "Display per-stage pipeline timing statistics"
        };
        statsOption.Aliases.Add("-s");

        var logFormatOption = new Option<string?>("--log-format")
        {
            Description = "Log output format (text or json). Defaults to text."
        };

        var (pluginOption, pluginDirOption, pluginRecursiveOption) = CreatePluginOptions();

        command.Options.Add(inputOption);
        command.Options.Add(dataOption);
        command.Options.Add(outputOption);
        command.Options.Add(logFileOption);
        command.Options.Add(logLevelOption);
        command.Options.Add(noLoggingOption);
        command.Options.Add(consoleLoggingOption);
        command.Options.Add(logFormatOption);
        command.Options.Add(configOption);
        command.Options.Add(statsOption);
        command.Options.Add(pluginOption);
        command.Options.Add(pluginDirOption);
        command.Options.Add(pluginRecursiveOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {

            // Build configuration with proper precedence: Command-line > Environment > Config file > Defaults
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(Directory.GetCurrentDirectory());

            // 1. Start with JSON config file (lowest priority)
            var configPath = parseResult.GetValue(configOption);
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = FindConfigurationFile();
            }

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                configBuilder.AddJsonFile(configPath, optional: false, reloadOnChange: false);
            }

            // 2. Add environment variables (override config file)
            configBuilder.AddEnvironmentVariables(prefix: "JYRO_");

            // 3. Add command-line arguments (highest priority)
            var cmdLineConfig = new Dictionary<string, string?>();

            if (parseResult.GetValue(inputOption) is string inputScript)
            {
                cmdLineConfig["InputScriptFile"] = inputScript;
            }

            if (parseResult.GetValue(dataOption) is string dataFile)
            {
                cmdLineConfig["DataJsonFile"] = dataFile;
            }

            if (parseResult.GetValue(outputOption) is string outputFile)
            {
                cmdLineConfig["OutputJsonFile"] = outputFile;
            }

            if (parseResult.GetValue(logFileOption) is string logFile)
            {
                cmdLineConfig["LogFile"] = logFile;
            }

            if (parseResult.GetValue(logLevelOption) is LogLevel logLevel)
            {
                cmdLineConfig["LogLevel"] = logLevel.ToString();
            }

            if (parseResult.GetValue(noLoggingOption))
            {
                cmdLineConfig["NoLogging"] = "true";
            }

            if (parseResult.GetValue(consoleLoggingOption) is bool consoleLogging)
            {
                cmdLineConfig["ConsoleLogging"] = consoleLogging.ToString();
            }

            if (parseResult.GetValue(statsOption))
            {
                cmdLineConfig["Stats"] = "true";
            }

            if (parseResult.GetValue(pluginOption) is string plugins)
            {
                cmdLineConfig["PluginAssemblies"] = plugins;
            }

            if (parseResult.GetValue(pluginDirOption) is string pluginDirs)
            {
                cmdLineConfig["PluginDirectories"] = pluginDirs;
            }

            if (parseResult.GetValue(pluginRecursiveOption) is string pluginRecursiveDirs)
            {
                cmdLineConfig["PluginRecursiveDirectories"] = pluginRecursiveDirs;
            }

            if (parseResult.GetValue(logFormatOption) is string logFormat)
            {
                cmdLineConfig["LogFormat"] = logFormat;
            }

            configBuilder.AddInMemoryCollection(cmdLineConfig!);

            var configuration = configBuilder.Build();

            // Check if InputScriptFile is provided
            var inputScriptFile = configuration["InputScriptFile"];
            if (string.IsNullOrWhiteSpace(inputScriptFile))
            {
                WriteError("Error: --input-script-file (-i) is required.");
                return 1;
            }

            // Build host with IOptions pattern
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<JyroCommandOptions>(opts => BindOptions(hostContext.Configuration, opts));
                    services.AddSingleton<IJyroScriptExecutor, JyroScriptExecutor>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    var opts = new JyroCommandOptions();
                    BindOptions(hostContext.Configuration, opts);

                    logging.ClearProviders();
                    if (opts.NoLogging || opts.LogLevel == LogLevel.None)
                    {
                        logging.SetMinimumLevel(LogLevel.None);
                        return;
                    }
                    var useJson = string.Equals(opts.LogFormat, "json", StringComparison.OrdinalIgnoreCase);
                    if (opts.ConsoleLogging)
                    {
                        if (useJson)
                        {
                            logging.AddJsonConsole(o =>
                            {
                                o.IncludeScopes = false;
                                o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";
                            });
                        }
                        else
                        {
                            logging.AddSimpleConsole(o =>
                            {
                                o.IncludeScopes = false;
                                o.SingleLine = true;
                                o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                            });
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(opts.LogFile))
                    {
                        logging.AddProvider(new SimpleFileLoggerProvider(opts.LogFile, useJson));
                    }

                    logging.SetMinimumLevel(opts.LogLevel);
                })
                .Build();

            var executor = host.Services.GetRequiredService<IJyroScriptExecutor>();
            return await executor.ExecuteAsync();
        });

        return command;
    }

    /// <summary>
    /// Creates the validate subcommand: jyro validate|v --compiled file.jyrx --raw file.jyro
    /// </summary>
    private static Command CreateValidateCommand()
    {
        var command = new Command("validate", "Verify a .jyrx binary is a correct compilation of a .jyro source file");
        command.Aliases.Add("v");

        var compiledOption = new Option<string>("--compiled")
        {
            Description = "Path to the compiled .jyrx binary file",
            Required = true
        };

        var rawOption = new Option<string>("--raw")
        {
            Description = "Path to the original .jyro source file",
            Required = true
        };

        var (logFileOption, verbosityOption, quietOption, consoleLoggingOption, logFormatOption) = CreateLoggingOptions();

        command.Options.Add(compiledOption);
        command.Options.Add(rawOption);
        command.Options.Add(logFileOption);
        command.Options.Add(verbosityOption);
        command.Options.Add(quietOption);
        command.Options.Add(consoleLoggingOption);
        command.Options.Add(logFormatOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var compiledPath = parseResult.GetValue(compiledOption)!;
            var rawPath = parseResult.GetValue(rawOption)!;

            using var loggerFactory = CreateLoggerFactory(
                parseResult.GetValue(logFileOption),
                parseResult.GetValue(verbosityOption),
                parseResult.GetValue(quietOption),
                parseResult.GetValue(consoleLoggingOption),
                parseResult.GetValue(logFormatOption));
            var logger = loggerFactory.CreateLogger("Jyro.Cli.Validate");

            if (!File.Exists(compiledPath))
            {
                WriteError($"Error: Compiled file not found: {compiledPath}");
                return 1;
            }

            if (!File.Exists(rawPath))
            {
                WriteError($"Error: Source file not found: {rawPath}");
                return 1;
            }

            logger.LogInformation("Validating {CompiledFile} against {SourceFile}", compiledPath, rawPath);

            // Read .jyrx header to get the stored source hash
            var jyrxBytes = await File.ReadAllBytesAsync(compiledPath);
            var headerResult = BinaryFormat.readHeader(jyrxBytes);

            if (!headerResult.IsSuccess)
            {
                PrintDiagnostics(headerResult.Messages);
                return 1;
            }

            // Compute SHA256 of the raw source file
            var source = await File.ReadAllTextAsync(rawPath);
            var sourceHash = SHA256.HashData(Encoding.UTF8.GetBytes(source));

            // Compare hashes
            var storedHash = headerResult.Value.Value.SourceHash;
            if (sourceHash.SequenceEqual(storedHash))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS]: {compiledPath} is a valid representation of {rawPath}");
                Console.ResetColor();
                logger.LogInformation("Validation passed");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL]: {compiledPath} does NOT match {rawPath}");
                Console.WriteLine($"  Source hash:   {Convert.ToHexString(sourceHash)}");
                Console.WriteLine($"  Compiled hash: {Convert.ToHexString(storedHash)}");
                Console.ResetColor();
                logger.LogWarning("Validation failed. Source hash: {SourceHash}, Compiled hash: {CompiledHash}",
                    Convert.ToHexString(sourceHash), Convert.ToHexString(storedHash));
                return 1;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the test subcommand: jyro test|t -i script.jyro|jyrx [-d data.json] -o expected.json
    /// </summary>
    private static Command CreateTestCommand()
    {
        var command = new Command("test", "Run a script and compare output against expected JSON");
        command.Aliases.Add("t");

        var inputOption = new Option<string>("--input-script-file")
        {
            Description = "Path to the Jyro script or .jyrx binary to execute",
            Required = true
        };
        inputOption.Aliases.Add("-i");

        var dataOption = new Option<string?>("--data-json-file")
        {
            Description = "JSON file providing the script's Data object (defaults to empty object)"
        };
        dataOption.Aliases.Add("-d");

        var expectedOutputOption = new Option<string>("--output-json-file")
        {
            Description = "Path to the expected output JSON file to compare against",
            Required = true
        };
        expectedOutputOption.Aliases.Add("-o");

        var testStatsOption = new Option<bool>("--stats")
        {
            Description = "Display per-stage pipeline timing statistics"
        };
        testStatsOption.Aliases.Add("-s");

        var (pluginOption, pluginDirOption, pluginRecursiveOption) = CreatePluginOptions();
        var (logFileOption, verbosityOption, quietOption, consoleLoggingOption, logFormatOption) = CreateLoggingOptions();

        command.Options.Add(inputOption);
        command.Options.Add(dataOption);
        command.Options.Add(expectedOutputOption);
        command.Options.Add(testStatsOption);
        command.Options.Add(pluginOption);
        command.Options.Add(pluginDirOption);
        command.Options.Add(pluginRecursiveOption);
        command.Options.Add(logFileOption);
        command.Options.Add(verbosityOption);
        command.Options.Add(quietOption);
        command.Options.Add(consoleLoggingOption);
        command.Options.Add(logFormatOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var inputFile = parseResult.GetValue(inputOption)!;
            var dataFile = parseResult.GetValue(dataOption);
            var expectedFile = parseResult.GetValue(expectedOutputOption)!;
            var showStats = parseResult.GetValue(testStatsOption);
            var plugins = parseResult.GetValue(pluginOption);
            var pluginDirs = parseResult.GetValue(pluginDirOption);
            var pluginRecursiveDirs = parseResult.GetValue(pluginRecursiveOption);

            using var loggerFactory = CreateLoggerFactory(
                parseResult.GetValue(logFileOption),
                parseResult.GetValue(verbosityOption),
                parseResult.GetValue(quietOption),
                parseResult.GetValue(consoleLoggingOption),
                parseResult.GetValue(logFormatOption),
                useStdErr: true);
            var logger = loggerFactory.CreateLogger("Jyro.Cli.Test");

            if (!File.Exists(inputFile))
            {
                WriteError($"Error: Input script file not found: {inputFile}");
                return 1;
            }

            if (!File.Exists(expectedFile))
            {
                WriteError($"Error: Expected output file not found: {expectedFile}");
                return 1;
            }

            logger.LogInformation("Testing {InputFile} against {ExpectedFile}", inputFile, expectedFile);

            // Load input data
            JyroValue data;
            if (!string.IsNullOrWhiteSpace(dataFile))
            {
                if (!File.Exists(dataFile))
                {
                    WriteError($"Error: Data file not found: {dataFile}");
                    return 1;
                }
                var dataJson = await File.ReadAllTextAsync(dataFile);
                data = JyroValue.FromJson(dataJson, FSharpOption<JsonSerializerOptions>.None);
            }
            else
            {
                data = JyroValue.FromJson("{}", FSharpOption<JsonSerializerOptions>.None);
            }

            // Build and execute
            var builder = new JyroBuilder()
                .WithData(data)
                .UseStdlib()
                .UseHttpFunctions()
                .AddFunction(new LogFunction(logger));

            // Load plugin functions
            try
            {
                var pluginFunctions = PluginLoader.LoadAll(plugins, pluginDirs, pluginRecursiveDirs).ToList();
                foreach (var fn in pluginFunctions)
                {
                    builder.AddFunction(fn);
                }

                if (pluginFunctions.Count > 0)
                {
                    logger.LogInformation("Loaded {Count} plugin function(s)", pluginFunctions.Count);
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error loading plugins: {ex.Message}");
                return 1;
            }

            JyroPipelineStats? stats = null;
            if (showStats)
            {
                stats = new JyroPipelineStats();
                builder = builder.WithStats(stats);
            }

            if (Path.GetExtension(inputFile).Equals(".jyrx", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await File.ReadAllBytesAsync(inputFile);
                builder = builder.WithCompiledBytes(bytes);
            }
            else
            {
                var source = await File.ReadAllTextAsync(inputFile);
                builder = builder.WithSource(source);
            }

            var result = builder.Execute();

            if (!result.IsSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL]: Execution of {inputFile} failed:");
                Console.ResetColor();
                PrintDiagnostics(result.Messages);
                return 1;
            }

            var resultValue = FSharpOption<JyroValue>.get_IsSome(result.Value)
                ? result.Value.Value
                : null;

            if (resultValue == null)
            {
                WriteError("[FAIL]: Execution returned null result.");
                return 1;
            }

            // Serialize actual output
            var actualJson = JyroScriptExecutor.SerializeJyroValue(resultValue.ToObjectValue());

            // Load expected output and compare
            var expectedJson = await File.ReadAllTextAsync(expectedFile);
            var mismatches = JsonComparer.Compare(expectedJson, actualJson);

            int exitCode;
            if (mismatches.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS]: {inputFile} correctly produced output identical to {expectedFile}");
                Console.ResetColor();
                logger.LogInformation("Test passed");
                exitCode = 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL]: {inputFile} produced output that differs from {expectedFile}");
                Console.ResetColor();
                Console.WriteLine($"  {mismatches.Count} difference(s) found:");
                foreach (var m in mismatches)
                {
                    Console.WriteLine($"    {m.Path}: expected {m.Expected}, got {m.Actual}");
                }
                logger.LogWarning("Test failed with {Count} difference(s)", mismatches.Count);
                exitCode = 1;
            }

            if (stats != null)
            {
                PrintPipelineStats(stats);
            }

            return exitCode;
        });

        return command;
    }

    /// <summary>
    /// Prints pipeline timing statistics to stderr.
    /// </summary>
    internal static void PrintPipelineStats(JyroPipelineStats stats)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Pipeline Statistics:");
        Console.Error.WriteLine("  ----------------------------");

        if (stats.IsFromJyrx)
        {
            Console.Error.WriteLine($"  Deserialize:  {stats.Deserialize.TotalMilliseconds,8:F2} ms");
        }
        else
        {
            Console.Error.WriteLine($"  Parse:        {stats.Parse.TotalMilliseconds,8:F2} ms");
            Console.Error.WriteLine($"  Validate:     {stats.Validate.TotalMilliseconds,8:F2} ms");
            Console.Error.WriteLine($"  Link:         {stats.Link.TotalMilliseconds,8:F2} ms");
        }

        Console.Error.WriteLine($"  Compile:      {stats.Compile.TotalMilliseconds,8:F2} ms");
        Console.Error.WriteLine($"  Execute:      {stats.Execute.TotalMilliseconds,8:F2} ms");
        Console.Error.WriteLine("  ----------------------------");
        Console.Error.WriteLine($"  Total:        {stats.Total.TotalMilliseconds,8:F2} ms");
    }

    /// <summary>
    /// Prints diagnostic messages to stderr in red.
    /// </summary>
    private static void PrintDiagnostics(IEnumerable<DiagnosticMessage> messages)
    {
        foreach (var msg in messages)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            var hasLocation = FSharpOption<SourceLocation>.get_IsSome(msg.Location);
            var location = hasLocation
                ? $" Line {msg.Location.Value.Line}, Column {msg.Location.Value.Column}:"
                : "";
            Console.WriteLine($"[{msg.Code}]{location} {msg.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Writes an error message to the console in red.
    /// </summary>
    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>
    /// Creates the shared plugin option definitions used by compile, run, and test commands.
    /// </summary>
    private static (Option<string?> plugin, Option<string?> pluginDir, Option<string?> pluginRecursive) CreatePluginOptions()
    {
        var pluginOption = new Option<string?>("--plugin")
        {
            Description = "Comma-separated paths to plugin assembly DLL files"
        };
        pluginOption.Aliases.Add("-p");

        var pluginDirOption = new Option<string?>("--plugin-directory")
        {
            Description = "Comma-separated directories to search for plugin DLLs (top-level only)"
        };
        pluginDirOption.Aliases.Add("-pd");

        var pluginRecursiveOption = new Option<string?>("--plugin-recursive")
        {
            Description = "Comma-separated directories to search recursively for plugin DLLs"
        };
        pluginRecursiveOption.Aliases.Add("-pr");

        return (pluginOption, pluginDirOption, pluginRecursiveOption);
    }

    /// <summary>
    /// Creates the shared logging option definitions used by compile, validate, and test commands.
    /// </summary>
    private static (Option<string?> logFile, Option<LogLevel?> verbosity, Option<bool> quiet, Option<bool?> consoleLogging, Option<string?> logFormat) CreateLoggingOptions()
    {
        var logFileOption = new Option<string?>("--log-file")
        {
            Description = "File for logging output (defaults to console only)"
        };
        logFileOption.Aliases.Add("-l");

        var verbosityOption = new Option<LogLevel?>("--verbosity")
        {
            Description = "Minimum log level (Trace, Debug, Information, Warning, Error, Critical, None)"
        };
        verbosityOption.Aliases.Add("-v");

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Disable all logging output"
        };
        quietOption.Aliases.Add("-q");

        var consoleLoggingOption = new Option<bool?>("--console-logging")
        {
            Description = "Enable console logging output (true/false). Default true."
        };

        var logFormatOption = new Option<string?>("--log-format")
        {
            Description = "Log output format (text or json). Defaults to text."
        };

        return (logFileOption, verbosityOption, quietOption, consoleLoggingOption, logFormatOption);
    }

    /// <summary>
    /// Creates a standalone ILoggerFactory configured with the given logging options.
    /// This mirrors the run command's Host-based logging setup but without requiring the Generic Host.
    /// </summary>
    private static ILoggerFactory CreateLoggerFactory(
        string? logFile, LogLevel? verbosity, bool quiet, bool? consoleLogging,
        string? logFormat = null, bool useStdErr = false)
    {
        var effectiveLevel = verbosity ?? LogLevel.Information;
        var enableConsole = consoleLogging ?? true;
        var useJson = string.Equals(logFormat, "json", StringComparison.OrdinalIgnoreCase);

        return LoggerFactory.Create(builder =>
        {
            if (quiet || effectiveLevel == LogLevel.None)
            {
                builder.SetMinimumLevel(LogLevel.None);
                return;
            }

            builder.SetMinimumLevel(effectiveLevel);

            if (enableConsole)
            {
                if (useJson)
                {
                    builder.AddJsonConsole(o =>
                    {
                        o.IncludeScopes = false;
                        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";
                    });
                }
                else
                {
                    builder.AddSimpleConsole(o =>
                    {
                        o.IncludeScopes = false;
                        o.SingleLine = true;
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                }

                if (useStdErr)
                {
                    builder.Services.Configure<ConsoleLoggerOptions>(o =>
                        o.LogToStandardErrorThreshold = LogLevel.Trace);
                }
            }

            if (!string.IsNullOrWhiteSpace(logFile))
            {
                builder.AddProvider(new SimpleFileLoggerProvider(logFile, useJson));
            }
        });
    }

    /// <summary>
    /// Manually binds configuration values to JyroCommandOptions without reflection.
    /// </summary>
    private static void BindOptions(IConfiguration config, JyroCommandOptions opts)
    {
        if (config["InputScriptFile"] is string inputScript)
        {
            opts.InputScriptFile = inputScript;
        }

        if (config["DataJsonFile"] is string dataJson)
        {
            opts.DataJsonFile = dataJson;
        }

        if (config["OutputJsonFile"] is string outputJson)
        {
            opts.OutputJsonFile = outputJson;
        }

        if (config["LogFile"] is string logFile)
        {
            opts.LogFile = logFile;
        }

        if (Enum.TryParse<LogLevel>(config["LogLevel"], ignoreCase: true, out var logLevel))
        {
            opts.LogLevel = logLevel;
        }

        if (bool.TryParse(config["NoLogging"], out var noLogging))
        {
            opts.NoLogging = noLogging;
        }

        if (bool.TryParse(config["ConsoleLogging"], out var consoleLogging))
        {
            opts.ConsoleLogging = consoleLogging;
        }

        if (config["LogFormat"] is string logFormat)
        {
            opts.LogFormat = logFormat;
        }

        if (bool.TryParse(config["Stats"], out var stats))
        {
            opts.Stats = stats;
        }

        if (config["PluginAssemblies"] is string pluginAssemblies)
        {
            opts.PluginAssemblies = pluginAssemblies;
        }

        if (config["PluginDirectories"] is string pluginDirectories)
        {
            opts.PluginDirectories = pluginDirectories;
        }

        if (config["PluginRecursiveDirectories"] is string pluginRecursiveDirectories)
        {
            opts.PluginRecursiveDirectories = pluginRecursiveDirectories;
        }
    }

    /// <summary>
    /// Searches for a configuration file in default locations.
    /// </summary>
    private static string? FindConfigurationFile()
    {
        const string defaultConfigFileName = "jyro.config.json";

        // 1. Current directory
        var currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), defaultConfigFileName);
        if (File.Exists(currentDirConfig))
        {
            return currentDirConfig;
        }

        // 2. AppData/Roaming/Jyro
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataConfig = Path.Combine(appDataPath, "Jyro", defaultConfigFileName);
        if (File.Exists(appDataConfig))
        {
            return appDataConfig;
        }

        // 3. User home directory
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeConfig = Path.Combine(homePath, defaultConfigFileName);
        if (File.Exists(homeConfig))
        {
            return homeConfig;
        }

        return null;
    }
}
