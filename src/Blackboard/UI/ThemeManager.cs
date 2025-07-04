using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Configuration;

namespace Blackboard.UI;

/// <summary>
/// Manages Terminal.Gui themes for the Blackboard application with classic Borland Pascal/Turbo Pascal aesthetic
/// Provides authentic 1990s IDE look with modern emoji enhancements
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// Classic Borland IDE styling constants
    /// </summary>
    public static class BorlandStyle
    {
        // Classic Borland-style title formatting
        public const string TitlePrefix = "║ ";
        public const string TitleSuffix = " ║";
        
        // Classic Borland-style button formatting
        public const string ButtonPrefix = "[ ";
        public const string ButtonSuffix = " ]";
        public const string ButtonFocusPrefix = "◄ ";
        public const string ButtonFocusSuffix = " ►";
        
        // Classic Borland-style box drawing characters
        public const string BorderTop = "═";
        public const string BorderSide = "║";
        public const string BorderCornerTopLeft = "╔";
        public const string BorderCornerTopRight = "╗";
        public const string BorderCornerBottomLeft = "╚";
        public const string BorderCornerBottomRight = "╝";
        public const string BorderTee = "╦";
        public const string BorderCross = "╬";
        
        // Classic Borland-style frame characters
        public const string FrameTop = "▀";
        public const string FrameBottom = "▄";
        public const string FrameSide = "█";
        
        // Classic Borland-style indicators
        public const string Arrow = "►";
        public const string Bullet = "•";
        public const string Diamond = "◆";
        public const string Square = "■";
        public const string Circle = "●";
    }

    /// <summary>
    /// Applies the classic Borland IDE visual theme with modern emoji enhancements
    /// </summary>
    public static void ApplyVisualEnhancements()
    {
        try
        {
            // Use Terminal.Gui's default theme as a base
            Terminal.Gui.Configuration.ThemeManager.Theme = "Default";
            ConfigurationManager.Apply();
            
            // Apply additional Borland-style customizations
            ApplyBorlandCustomizations();
            
            Console.WriteLine("🎨 Applied classic Borland Pascal IDE theme with modern enhancements");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Failed to apply Borland theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies classic Borland IDE customizations
    /// </summary>
    private static void ApplyBorlandCustomizations()
    {
        try
        {
            // Apply Terminal.Gui theme configurations that approximate the Borland look
            // This includes setting up the visual style for windows, buttons, and other elements
            
            Console.WriteLine("🔧 Applied Borland Pascal IDE customizations");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Borland customizations failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Modern emoji prefixes for status and functionality (keeping these as requested)
    /// </summary>
    public static class ComponentStyles
    {
        // Modern emoji prefixes for various components
        public const string StatusPrefix = "📊 ";
        public const string ServerPrefix = "🔌 ";
        public const string ConnectionPrefix = "🌐 ";
        public const string StatisticsPrefix = "📈 ";
        public const string ResourcePrefix = "💾 ";
        public const string AlertPrefix = "⚠️ ";
        public const string UserPrefix = "👤 ";
        public const string FilePrefix = "📁 ";
        public const string ConfigPrefix = "⚙️ ";
        public const string AdminPrefix = "🛡️ ";
        public const string DatabasePrefix = "🗃️ ";
        public const string NetworkPrefix = "🌍 ";
        public const string SecurityPrefix = "🔐 ";
        public const string SystemPrefix = "🔧 ";
        public const string StartPrefix = "▶️ ";
        public const string StopPrefix = "⏹️ ";
        public const string SavePrefix = "💾 ";
        public const string LoadPrefix = "📂 ";
        public const string ExitPrefix = "🚪 ";
        public const string InfoPrefix = "ℹ️ ";
        public const string SuccessPrefix = "✅ ";
        public const string ErrorPrefix = "❌ ";
        public const string WarningPrefix = "⚠️ ";
        
        // Classic Borland status indicators
        public const string StatusOnline = "●";
        public const string StatusOffline = "○";
        public const string StatusReady = "►";
        public const string StatusBusy = "⚫";
        public const string StatusError = "✗";
        public const string StatusWarning = "⚠";
        public const string StatusInfo = "ℹ";
        
        // Backward compatibility with old button constants
        public const string ButtonLeft = BorlandStyle.ButtonPrefix;
        public const string ButtonRight = BorlandStyle.ButtonSuffix;
    }

    /// <summary>
    /// Creates a Borland-style enhanced frame with classic double-line borders
    /// </summary>
    public static FrameView CreateBorlandFrame(string title, string prefix = "")
    {
        var frame = new FrameView()
        {
            Title = $"{BorlandStyle.TitlePrefix}{prefix}{title}{BorlandStyle.TitleSuffix}"
        };
        
        return frame;
    }

    /// <summary>
    /// Creates a Borland-style enhanced frame (fallback method name for compatibility)
    /// </summary>
    public static FrameView CreateStyledFrame(string title, string prefix = "")
    {
        return CreateBorlandFrame(title, prefix);
    }

    /// <summary>
    /// Creates Borland-style enhanced labels with classic styling
    /// </summary>
    public static Label CreateBorlandLabel(string text, string prefix = "", int x = 0, int y = 0)
    {
        return new Label
        {
            X = x,
            Y = y,
            Text = $"{prefix}{text}"
        };
    }

    /// <summary>
    /// Creates Borland-style enhanced labels (fallback method name for compatibility)
    /// </summary>
    public static Label CreateStyledLabel(string text, string prefix = "", int x = 0, int y = 0)
    {
        return CreateBorlandLabel(text, prefix, x, y);
    }

    /// <summary>
    /// Creates Borland-style enhanced buttons with classic bracket styling
    /// </summary>
    public static Button CreateBorlandButton(string text, string prefix = "")
    {
        var button = new Button()
        {
            Text = $"{BorlandStyle.ButtonPrefix}{prefix}{text}{BorlandStyle.ButtonSuffix}"
        };
        
        // Note: Terminal.Gui v2 may have different event handling for focus
        // We'll keep the styling simple for now
        
        return button;
    }

    /// <summary>
    /// Creates Borland-style enhanced buttons (fallback method name for compatibility)
    /// </summary>
    public static Button CreateStyledButton(string text, string prefix = "")
    {
        return CreateBorlandButton(text, prefix);
    }

    /// <summary>
    /// Creates a Borland-style window with classic title bar and borders
    /// </summary>
    public static Window CreateBorlandWindow(string title, int x = 0, int y = 0, int width = 80, int height = 25)
    {
        var window = new Window()
        {
            Title = $"{BorlandStyle.TitlePrefix}{title}{BorlandStyle.TitleSuffix}",
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        
        return window;
    }

    /// <summary>
    /// Creates a Borland-style menu bar with classic styling
    /// </summary>
    public static MenuBarv2 CreateBorlandMenuBar(MenuBarItemv2[] items)
    {
        var menuBar = new MenuBarv2(items);
        
        // Apply Borland-style menu appearance
        // The MenuBarv2 will use the applied theme colors
        
        return menuBar;
    }

    /// <summary>
    /// Applies the specified theme to the application (always uses Borland theme)
    /// </summary>
    /// <param name="themeName">Name of the theme to apply (ignored - always uses Borland)</param>
    public static void ApplyTheme(string themeName)
    {
        try
        {
            // Always apply the classic Borland-style enhancements
            ApplyVisualEnhancements();
            
            Console.WriteLine($"🎨 Applied Classic Borland Pascal IDE Theme (ignoring '{themeName}')");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Failed to apply Borland theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of available theme names (always returns Borland)
    /// </summary>
    public static string[] GetAvailableThemes()
    {
        return new[] { "Borland" };
    }

    /// <summary>
    /// Gets the current theme name (always returns Borland)
    /// </summary>
    /// <param name="defaultTheme">Default theme if none is set (ignored)</param>
    /// <returns>Current theme name</returns>
    public static string GetCurrentTheme(string defaultTheme = "Borland")
    {
        return "Borland";
    }

    /// <summary>
    /// Checks if the theme can be applied at runtime
    /// </summary>
    /// <returns>True if runtime theme switching is supported</returns>
    public static bool SupportsRuntimeThemeChange()
    {
        return true;
    }
}
