using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mesch.Jyro;

namespace Jyro.Cli;

/// <summary>
/// Loads IJyroFunction implementations from plugin assemblies.
/// </summary>
internal static class PluginLoader
{
    private const string PluginLoadingMessage = "Plugin loading requires reflection on external assemblies that are not subject to trimming.";

    /// <summary>
    /// Loads all IJyroFunction implementations from a DLL file at the specified path.
    /// </summary>
    /// <param name="assemblyPath">The file path to the assembly DLL to load.</param>
    /// <returns>All discovered IJyroFunction instances.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a function type cannot be instantiated.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PluginLoadingMessage)]
    public static IEnumerable<IJyroFunction> LoadFromAssembly(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}", assemblyPath);
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        return DiscoverFunctions(assembly, skipInstantiationErrors: false);
    }

    /// <summary>
    /// Loads all IJyroFunction implementations from all DLL files in the specified directory.
    /// Non-.NET assemblies and DLLs with missing dependencies are silently skipped.
    /// </summary>
    /// <param name="directoryPath">The directory path containing plugin DLL files.</param>
    /// <param name="searchOption">Whether to search subdirectories.</param>
    /// <returns>All discovered IJyroFunction instances.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PluginLoadingMessage)]
    public static IEnumerable<IJyroFunction> LoadFromDirectory(string directoryPath, SearchOption searchOption)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Plugin directory not found: {directoryPath}");
        }

        var functions = new List<IJyroFunction>();
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", searchOption);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                functions.AddRange(DiscoverFunctions(assembly, skipInstantiationErrors: true));
            }
            catch (BadImageFormatException)
            {
                // Skip non-.NET assemblies (e.g., native DLLs)
            }
            catch (Exception ex) when (ex is FileLoadException or FileNotFoundException)
            {
                // Skip DLLs that can't be loaded (e.g., missing dependencies)
            }
        }

        return functions;
    }

    /// <summary>
    /// Loads all plugin functions from the specified assemblies and directories.
    /// All parameters accept comma-separated values.
    /// </summary>
    /// <param name="assemblies">Comma-separated DLL file paths, or null.</param>
    /// <param name="directories">Comma-separated directory paths (top-level search), or null.</param>
    /// <param name="recursiveDirectories">Comma-separated directory paths (recursive search), or null.</param>
    /// <returns>All discovered IJyroFunction instances.</returns>
    public static IEnumerable<IJyroFunction> LoadAll(
        string? assemblies,
        string? directories,
        string? recursiveDirectories)
    {
        var functions = new List<IJyroFunction>();

        if (!string.IsNullOrWhiteSpace(assemblies))
        {
            foreach (var path in SplitPaths(assemblies))
            {
                functions.AddRange(LoadFromAssembly(path));
            }
        }

        if (!string.IsNullOrWhiteSpace(directories))
        {
            foreach (var dir in SplitPaths(directories))
            {
                functions.AddRange(LoadFromDirectory(dir, SearchOption.TopDirectoryOnly));
            }
        }

        if (!string.IsNullOrWhiteSpace(recursiveDirectories))
        {
            foreach (var dir in SplitPaths(recursiveDirectories))
            {
                functions.AddRange(LoadFromDirectory(dir, SearchOption.AllDirectories));
            }
        }

        return functions;
    }

    /// <summary>
    /// Discovers all concrete IJyroFunction implementations in an assembly and instantiates them.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PluginLoadingMessage)]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = PluginLoadingMessage)]
    private static IEnumerable<IJyroFunction> DiscoverFunctions(Assembly assembly, bool skipInstantiationErrors)
    {
        var functionTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IJyroFunction).IsAssignableFrom(t));

        var functions = new List<IJyroFunction>();

        foreach (var functionType in functionTypes)
        {
            try
            {
                if (Activator.CreateInstance(functionType) is IJyroFunction instance)
                {
                    functions.Add(instance);
                }
            }
            catch (Exception ex)
            {
                if (skipInstantiationErrors)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Failed to instantiate JyroFunction type '{functionType.FullName}'. " +
                    $"Ensure the type has a public parameterless constructor. Inner exception: {ex.Message}",
                    ex);
            }
        }

        return functions;
    }

    /// <summary>
    /// Splits a comma-separated string into trimmed, non-empty paths.
    /// </summary>
    private static IEnumerable<string> SplitPaths(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
