using Microsoft.Extensions.Configuration;

namespace Blackboard.Core.Configuration;

/// <summary>
///     Utility class to resolve configuration paths with the root path variable
/// </summary>
public static class PathResolver
{
    /// <summary>
    ///     Resolves any string containing {RootPath} using the configured system:rootPath value.
    /// </summary>
    /// <param name="value">The string value to resolve.</param>
    /// <param name="configuration">The configuration object to read system:rootPath from.</param>
    /// <returns>The resolved string with {RootPath} replaced.</returns>
    public static string ResolveWithConfig(string value, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        var rootPath = configuration["system:rootPath"];
        if (string.IsNullOrEmpty(rootPath))
            throw new InvalidOperationException("system:rootPath is not configured");
        return value.Replace("{RootPath}", rootPath);
    }

    /// <summary>
    ///     Recursively resolves all string values in a configuration section, replacing {RootPath} tokens.
    /// </summary>
    /// <param name="section">The configuration section to process.</param>
    /// <param name="configuration">The configuration object to read the root path from.</param>
    /// <remarks>This does not mutate the configuration, but can be used to enumerate and resolve all values.</remarks>
    public static void ResolveSectionPaths(IConfigurationSection section, IConfiguration configuration, Action<string, string> onResolved)
    {
        foreach (var child in section.GetChildren())
            if (!child.GetChildren().Any())
            {
                var value = child.Value;
                if (!string.IsNullOrEmpty(value) && value.Contains("{RootPath}"))
                {
                    var resolved = ResolveWithConfig(value, configuration);
                    onResolved(child.Path, resolved);
                }
            }
            else
            {
                ResolveSectionPaths(child, configuration, onResolved);
            }
    }

    /// <summary>
    ///     Resolves a path string with the {RootPath} placeholder
    /// </summary>
    /// <param name="path">Path with potential {RootPath} placeholder</param>
    /// <param name="rootPath">Root path to substitute</param>
    /// <returns>Resolved path</returns>
    public static string ResolvePath(string path, string rootPath)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace the placeholder with the actual root path
        var resolvedPath = path.Replace("{RootPath}", rootPath);

        // Check if the path is relative (doesn't start with / or C:\ etc)
        if (!Path.IsPathRooted(resolvedPath))
            // If it's still relative after replacement, prepend the rootPath
            resolvedPath = Path.Combine(rootPath, resolvedPath);

        return resolvedPath;
    }

    /// <summary>
    ///     Resolves a connection string with the {RootPath} placeholder
    /// </summary>
    /// <param name="connectionString">Connection string with potential {RootPath} placeholder</param>
    /// <param name="rootPath">Root path to substitute</param>
    /// <returns>Resolved connection string</returns>
    public static string ResolveConnectionString(string connectionString, string rootPath)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        // Replace the placeholder with the actual root path
        return connectionString.Replace("{RootPath}", rootPath);
    }

    /// <summary>
    ///     Ensures the directory for a path exists
    /// </summary>
    /// <param name="path">The file path whose directory should exist</param>
    public static void EnsureDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Resolves the root path, handling special cases like ".", "~", and relative paths
    /// </summary>
    /// <param name="rootPath">The configured root path</param>
    /// <returns>Absolute path for the root directory</returns>
    public static string ResolveRootPath(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath))
            return Directory.GetCurrentDirectory();

        // Handle special cases
        if (rootPath == "." || rootPath == "./") return Directory.GetCurrentDirectory();

        if (rootPath.StartsWith("~/"))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, rootPath.Substring(2));
        }

        if (rootPath == "~") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // If it's already an absolute path, use it as-is
        if (Path.IsPathRooted(rootPath)) return rootPath;

        // For relative paths, make them relative to the application directory
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.GetFullPath(Path.Combine(appDirectory, rootPath));
    }
}