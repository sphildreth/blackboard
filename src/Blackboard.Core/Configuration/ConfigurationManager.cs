using YamlDotNet.Serialization;
using Serilog;

namespace Blackboard.Core.Configuration;

public class ConfigurationManager
{
    public const string FilesPath = "files";
    public const string DatabasePath = "database";
    public const string ScreensPath = "screens";
    public const string DatabaseBackupPath = "database/backup";
    public const string LogsPath = "logs";
    
    private readonly string _configFilePath;
    private readonly ILogger _logger;
    private SystemConfiguration? _configuration;
    private FileSystemWatcher? _fileWatcher;
    
    public event EventHandler<SystemConfiguration>? ConfigurationChanged;

    public ConfigurationManager(string configFilePath, ILogger logger)
    {
        _configFilePath = configFilePath;
        _logger = logger;
        LoadConfiguration();
        SetupFileWatcher();
    }

    public SystemConfiguration Configuration => _configuration ?? new SystemConfiguration();

    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.Information("Configuration file not found, creating default configuration at {FilePath}", _configFilePath);
                CreateDefaultConfiguration();
                return;
            }

            _logger.Information("Loading configuration from {FilePath}", _configFilePath);
            var yaml = File.ReadAllText(_configFilePath);
            _logger.Debug("YAML file content:\n{YamlContent}", yaml);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<SystemConfiguration>(yaml);
            if (config == null)
            {
                _logger.Warning("Deserialized configuration is null, using default configuration");
                _configuration = new SystemConfiguration();
            }
            else
            {
                _configuration = config;
            }
            _logger.Information("Configuration loaded successfully from {FilePath}", _configFilePath);
        }
        catch (YamlDotNet.Core.YamlException yamlEx)
        {
            _logger.Error(yamlEx, "YAML parsing error in {FilePath}", _configFilePath);
            _configuration = new SystemConfiguration();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load configuration from {FilePath}", _configFilePath);
            _configuration = new SystemConfiguration();
        }
    }

    private void CreateDefaultConfiguration()
    {
        _configuration = new SystemConfiguration();
        SaveConfiguration();
    }

    public void SaveConfiguration()
    {
        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(_configuration);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_configFilePath, yaml);
            _logger.Information("Configuration saved to {FilePath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save configuration to {FilePath}", _configFilePath);
        }
    }

    private void SetupFileWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            var fileName = Path.GetFileName(_configFilePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                return;

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnConfigFileChanged;
            _logger.Information("Configuration file watcher enabled for {FilePath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to setup configuration file watcher for {FilePath}", _configFilePath);
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce file changes
            Thread.Sleep(100);
            
            _logger.Information("Configuration file changed, reloading...");
            var oldConfig = _configuration;
            LoadConfiguration();
            
            if (_configuration != null)
            {
                ConfigurationChanged?.Invoke(this, _configuration);
                _logger.Information("Configuration reloaded successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reload configuration after file change");
        }
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}
