using System.CommandLine;

namespace Jyro.Cli;

/// <summary>
/// Entry point for the Jyro console application.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && !IsKnownFirstArg(args[0]))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("A subcommand is required. Available commands: run (r), compile (c), validate (v), test (t)");
            Console.ResetColor();
            Console.WriteLine("Use 'jyro --help' for more information.");
            return 1;
        }

        try
        {
            var commandFactory = new JyroCommandFactory();
            var rootCommand = commandFactory.CreateRootCommand();
            return await rootCommand.InvokeAsync(args);
        }
        catch (FileNotFoundException ex)
        {
            WriteError($"Error: File not found: {ex.FileName ?? ex.Message}");
            return 1;
        }
        catch (DirectoryNotFoundException ex)
        {
            WriteError($"Error: Directory not found: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteError($"Error: Access denied: {ex.Message}");
            return 1;
        }
        catch (System.Text.Json.JsonException ex)
        {
            WriteError($"Error: Invalid JSON: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            WriteError($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static bool IsKnownFirstArg(string arg) =>
        arg is "run" or "r" or "compile" or "c" or "validate" or "v" or "test" or "t"
            or "-h" or "--help" or "-?" or "--version";
}
