using System.Net;

namespace Blackboard.Core.Network;

public interface ITelnetConnection
{
    EndPoint? RemoteEndPoint { get; }
    string RemoteEndPointString { get; }
    bool IsConnected { get; }
    bool SupportsAnsi { get; }
    bool SupportsCP437 { get; }
    bool IsModernTerminal { get; }
    bool UserRequestedAnsi { get; }
    string ClientSoftware { get; }
    string TerminalType { get; }
    DateTime ConnectedAt { get; }
    event EventHandler? Disconnected;

    Task InitializeAsync();
    Task SendAsync(string data);
    Task SendBytesAsync(byte[] data);
    Task SendLineAsync(string line);
    Task SendAnsiAsync(string ansiSequence);
    Task<string> ReadLineAsync();
    Task<char> ReadCharAsync();
    Task DisconnectAsync();
    void EnableAnsiMode();
    void DisableAnsiMode();
    void Dispose();
}