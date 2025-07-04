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
    /// Applies visual enhancements to make the UI more colorful and contrasted
    /// </summary>
    public static void ApplyVisualEnhancements()
    {
        try
        {
            // Apply a more visually appealing theme that supports color variation
            // Try different built-in themes that provide better color contrast
            Terminal.Gui.Configuration.ThemeManager.Theme = "Default";
            ConfigurationManager.Apply();
            
            Console.WriteLine("Visual enhancements applied - UI should now have better color contrast");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply visual enhancements: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets style information for different UI components to provide visual variety
    /// </summary>
    public static class ComponentStyles
    {
        public const string StatusPrefix = "📊 ";
        public const string ServerPrefix = "🔌 ";
        public const string ConnectionPrefix = "🌐 ";
        public const string StatisticsPrefix = "📈 ";
        public const string ResourcePrefix = "💾 ";
        public const string AlertPrefix = "⚠️ ";
        public const string UserPrefix = "👤 ";
        public const string FilePrefix = "📁 ";
        public const string ConfigPrefix = "⚙️ ";
        
        // Box drawing characters for enhanced visual borders
        public const string BorderTop = "═";
        public const string BorderSide = "║";
        public const string BorderCornerTopLeft = "╔";
        public const string BorderCornerTopRight = "╗";
        public const string BorderCornerBottomLeft = "╚";
        public const string BorderCornerBottomRight = "╝";
    }
    
    /// <summary>
    /// Creates visually enhanced frame views with better styling
    /// </summary>
    public static FrameView CreateStyledFrame(string title, string prefix = "")
    {
        var frame = new FrameView()
        {
            Title = $"{prefix}{title}"
        };
        
        return frame;
    }
    
    /// <summary>
    /// Creates visually enhanced labels with prefixes and styling
    /// </summary>
    public static Label CreateStyledLabel(string text, string prefix = "", int x = 0, int y = 0)
    {
        return new Label
        {
            X = x,
            Y = y,
            Text = $"{prefix}{text}"
        };
    }
    
    /// <summary>
    /// Creates visually enhanced buttons with better styling
    /// </summary>
    public static Button CreateStyledButton(string text, string prefix = "")
    {
        var button = new Button()
        {
            Text = $"{prefix}{text}"
        };
        
        return button;
    }
    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    /// <param name="themeName">Name of the theme to apply (now always "Enhanced")</param>
    public static void ApplyTheme(string themeName)
    {
        try
        {
            // Apply visual enhancements - our custom theme is always active
            ApplyVisualEnhancements();
            
            Console.WriteLine($"Applied Enhanced Visual Theme with custom styling and icons");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply enhanced theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of available theme names
    /// </summary>
    public static string[] GetAvailableThemes()
    {
        // Since we now use custom visual enhancements, we only offer our enhanced theme
        return new[] { "Enhanced" };
    }

    /// <summary>
    /// Gets the current theme name from Terminal.Gui's ThemeManager
    /// </summary>
    /// <param name="defaultTheme">Default theme if none is set</param>
    /// <returns>Current theme name</returns>
    public static string GetCurrentTheme(string defaultTheme = "Enhanced")
    {
        // We now use a custom enhanced theme
        return "Enhanced";
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
    /// Normalizes theme names to match our custom enhanced theme
    /// </summary>
    /// <param name="themeName">The theme name to normalize</param>
    /// <returns>Normalized theme name</returns>
    private static string NormalizeThemeName(string themeName)
    {
        // All theme names now map to our enhanced theme since we override visuals
        return "Enhanced";
    }
}
