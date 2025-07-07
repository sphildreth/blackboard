using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Serilog;

namespace Blackboard.Core.Network;

public class MenuOption
{
    public required string Label { get; set; }
    public required string Screen { get; set; }
    public required string Action { get; set; }
}

public class MenuConfig
{
    public required string Screen { get; set; }
    public required string Prompt { get; set; }
    public required Dictionary<string, MenuOption> Options { get; set; }
}

public static class MenuConfigLoader
{
    public static async Task<MenuConfig> LoadAsync(string menuConfigPath)
    {
        using var reader = new StreamReader(menuConfigPath);
        var yaml = await reader.ReadToEndAsync();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<MenuConfig>(yaml);
    }

    public static async Task<string> LoadScreenAsync(string screensDir, string screenFile, bool preferAnsi = false)
    {
        var path = Path.Combine(screensDir, screenFile);
        
        // If the screen file has an extension, use it directly
        if (Path.HasExtension(screenFile))
        {
            if (!File.Exists(path))
                return $"[Screen file not found: {screenFile}]";
            
            var extension = Path.GetExtension(screenFile).ToLowerInvariant();
            if (extension == ".ans")
            {
                // Read ANSI file with proper encoding for raw bytes
                var fileBytes = await File.ReadAllBytesAsync(path);
                return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
            }
            else
            {
                // Read ASCII file as UTF-8 text
                return await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
            }
        }
        
        // No extension provided, try ASCII first, then ANSI
        var baseFileName = Path.GetFileNameWithoutExtension(screenFile);
        var directory = Path.GetDirectoryName(path) ?? screensDir;
        
        var asciiFile = Path.Combine(directory, $"{baseFileName}.asc");
        var ansiFile = Path.Combine(directory, $"{baseFileName}.ans");

        if (preferAnsi && File.Exists(ansiFile))
        {
            var fileBytes = await File.ReadAllBytesAsync(ansiFile);
            return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }
        else if (File.Exists(asciiFile))
        {
            return await File.ReadAllTextAsync(asciiFile, System.Text.Encoding.UTF8);
        }
        else if (File.Exists(ansiFile))
        {
            var fileBytes = await File.ReadAllBytesAsync(ansiFile);
            return System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }
        
        return $"[Screen file not found: {screenFile}]";
    }
}