using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Blackboard.Core.Network
{
    public class MenuOption
    {
        public string Label { get; set; }
        public string Screen { get; set; }
        public string Action { get; set; }
    }

    public class MenuConfig
    {
        public string Screen { get; set; }
        public string Prompt { get; set; }
        public Dictionary<string, MenuOption> Options { get; set; }
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
}
