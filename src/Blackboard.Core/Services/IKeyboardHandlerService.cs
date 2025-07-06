using Blackboard.Core.Network;

namespace Blackboard.Core.Services;

public interface IKeyboardHandlerService
{
    /// <summary>
    ///     Reads a single key input with support for special keys
    /// </summary>
    Task<KeyInput> ReadKeyAsync(ITelnetConnection connection);

    /// <summary>
    ///     Reads a line with character echo control
    /// </summary>
    Task<string> ReadLineAsync(ITelnetConnection connection, bool echoInput = true);

    /// <summary>
    ///     Checks if the key is a special function key
    /// </summary>
    bool IsSpecialKey(KeyInput key);

    /// <summary>
    ///     Enables or disables local echo
    /// </summary>
    Task SetEchoModeAsync(ITelnetConnection connection, bool enabled);
}

/// <summary>
///     Represents keyboard input with support for special keys
/// </summary>
public class KeyInput
{
    public KeyInput(char character)
    {
        Character = character;
        SpecialKey = SpecialKey.None;
    }

    public KeyInput(SpecialKey specialKey)
    {
        Character = '\0';
        SpecialKey = specialKey;
    }

    public char Character { get; set; }
    public SpecialKey SpecialKey { get; set; }
    public bool IsSpecial => SpecialKey != SpecialKey.None;
}

/// <summary>
///     Special key codes for enhanced navigation
/// </summary>
public enum SpecialKey
{
    None,
    Enter,
    Escape,
    Backspace,
    Delete,
    Tab,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,
    Home,
    End,
    PageUp,
    PageDown,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12
}