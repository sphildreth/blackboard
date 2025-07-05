namespace Blackboard.Core.Configuration;

public class NetworkSettings
{
    public string TelnetBindAddress { get; set; } = "0.0.0.0";
    public int TelnetPort { get; set; } = 23;
    public int MaxConcurrentConnections { get; set; } = 10;
    public int ConnectionTimeoutSeconds { get; set; } = 300;
    public string TelnetEncoding { get; set; } = "ASCII"; // ASCII, UTF-8, or CP437
}