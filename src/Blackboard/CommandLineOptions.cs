using CommandLine;

namespace Blackboard;

/// <summary>
///     Command-line options for the Blackboard application.
/// </summary>
public class CommandLineOptions
{
    [Option('c', "console", Required = false, HelpText = "Run in console mode without Terminal.Gui interface.")]
    public bool ConsoleMode { get; set; }

    [Option('p', "port", Required = false, HelpText = "Override the telnet server port.")]
    public int? Port { get; set; }

    [Option("config", Required = false, HelpText = "Specify a custom configuration file path.")]
    public string? ConfigPath { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose logging.")]
    public bool Verbose { get; set; }

    [Option("no-server", Required = false, HelpText = "Do not start the telnet server automatically.")]
    public bool NoServer { get; set; }

    [Option("version", Required = false, HelpText = "Display version information and exit.")]
    public bool Version { get; set; }

    // Future options can be added here:
    // [Option("backup", Required = false, HelpText = "Perform database backup and exit.")]
    // public bool Backup { get; set; }

    // [Option("maintenance", Required = false, HelpText = "Run in maintenance mode.")]
    // public bool MaintenanceMode { get; set; }
}