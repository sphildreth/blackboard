using System;
using System.IO;

namespace Blackboard.Core.Configuration;

/// <summary>
/// Utility class to resolve configuration paths with the root path variable
/// </summary>
public static class PathResolver
{
    /// <summary>
    /// Resolves a path string with the {RootPath} placeholder
    /// </summary>
    /// <param name="path">Path with potential {RootPath} placeholder</param>
    /// <param name="rootPath">Root path to substitute</param>
    /// <returns>Resolved path</returns>
    public static string ResolvePath(string path, string rootPath)
    {
        if (string.IsNullOrEmpty(path))
            return path;
            
        // Replace the placeholder with the actual root path
        string resolvedPath = path.Replace("{RootPath}", rootPath);
        
        // Check if the path is relative (doesn't start with / or C:\ etc)
        if (!Path.IsPathRooted(resolvedPath))
        {
            // If it's still relative after replacement, prepend the rootPath
            resolvedPath = Path.Combine(rootPath, resolvedPath);
        }
        
        return resolvedPath;
    }
    
    /// <summary>
    /// Resolves a connection string with the {RootPath} placeholder
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
    /// Ensures the directory for a path exists
    /// </summary>
    /// <param name="path">The file path whose directory should exist</param>
    public static void EnsureDirectoryExists(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
