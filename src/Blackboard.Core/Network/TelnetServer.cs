using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;
using Blackboard.Core.Configuration;

namespace Blackboard.Core.Network;

public class TelnetServer
{
    private readonly ILogger _logger;
    private readonly ConfigurationManager _configManager;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<TelnetConnection> _activeConnections;
    private bool _isRunning;

    public event EventHandler<TelnetConnection>? ClientConnected;
    public event EventHandler<TelnetConnection>? ClientDisconnected;

    public IReadOnlyList<TelnetConnection> ActiveConnections => _activeConnections.AsReadOnly();
    public bool IsRunning => _isRunning;

    public TelnetServer(ILogger logger, ConfigurationManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _activeConnections = new List<TelnetConnection>();
    }

    public Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.Warning("Telnet server is already running");
            return Task.CompletedTask;
        }

        try
        {
            var config = _configManager.Configuration.Network;
            var bindAddress = IPAddress.Parse(config.TelnetBindAddress);
            
            _listener = new TcpListener(bindAddress, config.TelnetPort);
            _listener.Start();
            
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            _logger.Information("Telnet server started on {Address}:{Port}", bindAddress, config.TelnetPort);

            // Start accepting connections
            _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start Telnet server");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _logger.Information("Stopping Telnet server...");

        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();

        // Disconnect all active connections
        var connectionsCopy = _activeConnections.ToList();
        foreach (var connection in connectionsCopy)
        {
            await connection.DisconnectAsync();
        }

        _activeConnections.Clear();
        _logger.Information("Telnet server stopped");
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                var config = _configManager.Configuration.Network;

                // Check connection limits
                if (_activeConnections.Count >= config.MaxConcurrentConnections)
                {
                    _logger.Warning("Connection rejected: Maximum concurrent connections reached ({Max})", 
                        config.MaxConcurrentConnections);
                    tcpClient.Close();
                    continue;
                }

                var connection = new TelnetConnection(tcpClient, _logger, config.ConnectionTimeoutSeconds);
                _activeConnections.Add(connection);

                connection.Disconnected += (sender, args) =>
                {
                    _activeConnections.Remove(connection);
                    ClientDisconnected?.Invoke(this, connection);
                };

                ClientConnected?.Invoke(this, connection);
                _logger.Information("New telnet connection from {RemoteEndPoint}", connection.RemoteEndPoint);

                // Start handling the connection
                _ = Task.Run(() => HandleConnectionAsync(connection, cancellationToken));
            }
            catch (ObjectDisposedException)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error accepting telnet connection");
            }
        }
    }

    private async Task HandleConnectionAsync(TelnetConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.InitializeAsync();
            
            // Send welcome message with ANSI formatting
            await connection.SendAnsiAsync("\x1b[2J\x1b[H"); // Clear screen and home cursor
            await connection.SendAnsiAsync("\x1b[1;36m"); // Bright cyan
            await connection.SendLineAsync("Welcome to " + _configManager.Configuration.System.BoardName);
            await connection.SendAnsiAsync("\x1b[0m"); // Reset colors
            await connection.SendLineAsync("");

            // Handle the session (this will be expanded in later phases)
            await HandleSessionAsync(connection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling telnet connection from {RemoteEndPoint}", connection.RemoteEndPoint);
        }
        finally
        {
            await connection.DisconnectAsync();
        }
    }

    private async Task HandleSessionAsync(TelnetConnection connection, CancellationToken cancellationToken)
    {
        // Basic session handling - will be expanded in Phase 2
        await connection.SendLineAsync("Session handling not yet implemented.");
        await connection.SendLineAsync("Press any key to disconnect...");
        await connection.ReadLineAsync();
    }
}
