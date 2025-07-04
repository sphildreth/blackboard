namespace Blackboard.Core.Configuration;

public class SystemSettings
{
    public string BoardName { get; set; } = "Blackboard";
    public string SysopName { get; set; } = "System Operator";
    public string Location { get; set; } = "Somewhere, USA";
    public bool SystemOnline { get; set; } = false;
    public bool RequirePreEnterCode { get; set; } = false;
    public string PreEnterCode { get; set; } = string.Empty;
    public int MaxUsers { get; set; } = 100;
    public string TimeZone { get; set; } = "UTC";
    /// <summary>
    /// The root folder for the application's main storage point (menus, doors, etc.)
    /// </summary>
    public string RootPath { get; set; } = "data";

    /// <summary>
    /// The folder under RootPath where menu/screen files are stored
    /// </summary>
    public string ScreensPath { get; set; } = "screens";
    
    /// <summary>
    /// The folder under RootPath where file area files are stored
    /// </summary>
    public string FilesPath { get; set; } = "files";
    
    /// <summary>
    /// Whether the terminal server should start automatically at application startup
    /// </summary>
    public bool TerminalServerAutoStart { get; set; } = true;
    
    /// <summary>
    /// The UI theme to use for the application interface
    /// Note: Currently uses "Borland" theme with classic 1990s IDE aesthetic and modern emoji icons
    /// </summary>
    public string Theme { get; set; } = "Borland";
}