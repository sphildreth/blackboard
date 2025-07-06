using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

    public static async Task<string> LoadScreenAsync(string screensDir, string screenFile)
    {
        var path = Path.Combine(screensDir, screenFile);
        if (!File.Exists(path))
            return $"[Screen file not found: {screenFile}]";
        return await File.ReadAllTextAsync(path);
    }
}