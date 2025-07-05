using System.Net;

namespace Blackboard.Core.Network;

public interface ITelnetConnection
{
    event EventHandler? Disconnected;
    
    EndPoint? RemoteEndPoint { get; }
    string RemoteEndPointString { get; }
    bool IsConnected { get; }
    bool SupportsAnsi { get; }
    string TerminalType { get; }
    DateTime ConnectedAt { get; }
    
    Task InitializeAsync();
    Task SendAsync(string data);
    Task SendLineAsync(string line);
    Task SendAnsiAsync(string ansiSequence);
    Task<string> ReadLineAsync();
    Task<char> ReadCharAsync();
    Task DisconnectAsync();
    void Dispose();
}
