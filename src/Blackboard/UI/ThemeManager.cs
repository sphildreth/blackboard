using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Configuration;

namespace Blackboard.UI;

/// <summary>
/// Manages Terminal.Gui themes for the Blackboard application using Terminal.Gui v2 ConfigurationManager
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    /// <param name="themeName">Name of the theme to apply</param>
    public static void ApplyTheme(string themeName)
    {
        try
        {
            // Normalize theme name to match Terminal.Gui's built-in themes
            var normalizedTheme = NormalizeThemeName(themeName);
            
            Console.WriteLine($"Applying theme: {normalizedTheme}");
            
            // Set the theme using Terminal.Gui v2's ThemeManager
            Terminal.Gui.Configuration.ThemeManager.Theme = normalizedTheme;
            
            // Apply the configuration
            ConfigurationManager.Apply();
            
            Console.WriteLine($"Theme '{normalizedTheme}' applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply theme '{themeName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of available theme names
    /// </summary>
    public static string[] GetAvailableThemes()
    {
        try
        {
            // Get themes from Terminal.Gui's ThemeManager
            var themeNames = Terminal.Gui.Configuration.ThemeManager.GetThemeNames();
            return themeNames.ToArray();
        }
        catch (Exception)
        {
            // Fallback to known built-in themes
            return new[] { "Default", "Dark", "Light" };
        }
    }

    /// <summary>
    /// Gets the current theme name from Terminal.Gui's ThemeManager
    /// </summary>
    /// <param name="defaultTheme">Default theme if none is set</param>
    /// <returns>Current theme name</returns>
    public static string GetCurrentTheme(string defaultTheme = "Default")
    {
        try
        {
            return Terminal.Gui.Configuration.ThemeManager.GetCurrentThemeName() ?? defaultTheme;
        }
        catch (Exception)
        {
            return defaultTheme;
        }
    }

    /// <summary>
    /// Checks if the theme can be applied at runtime
    /// </summary>
    /// <returns>True if runtime theme switching is supported</returns>
    public static bool SupportsRuntimeThemeChange()
    {
        // Terminal.Gui v2 supports runtime theme changes
        return true;
    }

    /// <summary>
    /// Normalizes theme names to match Terminal.Gui's built-in themes
    /// </summary>
    /// <param name="themeName">The theme name to normalize</param>
    /// <returns>Normalized theme name</returns>
    private static string NormalizeThemeName(string themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            return "Default";

        return themeName.ToLowerInvariant() switch
        {
            "dark" => "Dark",
            "light" => "Light", 
            "default" => "Default",
            "modern" => "Default", // Map modern to default
            "terminal" => "Default", // Map terminal to default
            "custom" => "Default", // Map custom to default for now
            "blackboard" => "Default", // Map blackboard to default for now
            _ => "Default"
        };
    }
}
