namespace Jyro.Cli;

/// <summary>
/// Interface for script execution.
/// </summary>
public interface IJyroScriptExecutor
{
    /// <summary>
    /// Executes the Jyro script according to the configured options.
    /// </summary>
    /// <returns>A task containing the exit code: 0 for success, non-zero for failure.</returns>
    Task<int> ExecuteAsync();
}
