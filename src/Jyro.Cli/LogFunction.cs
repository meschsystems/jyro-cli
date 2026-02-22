using Mesch.Jyro;
using Microsoft.Extensions.Logging;

namespace Jyro.Cli;

/// <summary>
/// Host function that lets Jyro scripts write to the CLI logging pipeline.
/// Usage: Log("Info", "message text")
/// </summary>
internal sealed class LogFunction : JyroFunctionBase
{
    private readonly ILogger _logger;

    public LogFunction(ILogger logger)
        : base("Log", FunctionSignatures.create(
            "Log",
            Microsoft.FSharp.Collections.ListModule.OfSeq([
                Parameter.Required("level", ParameterType.StringParam),
                Parameter.Required("message", ParameterType.StringParam)
            ]),
            ParameterType.NullParam))
    {
        _logger = logger;
    }

    public override JyroValue ExecuteImpl(IReadOnlyList<JyroValue> args, JyroExecutionContext ctx)
    {
        var levelStr = GetStringArgument(args, 0);
        var message = GetStringArgument(args, 1);

        var logLevel = ParseLogLevel(levelStr);
        if (_logger.IsEnabled(logLevel))
        {
            _logger.Log(logLevel, "[Script] {Message}", message);
        }

        return JyroNull.Instance;
    }

    private static LogLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => throw new JyroRuntimeException(
                MessageCode.InvalidArgumentType,
                $"Invalid log level '{level}'. Valid values: Trace, Debug, Info/Information, Warn/Warning, Error, Critical")
        };
    }
}
