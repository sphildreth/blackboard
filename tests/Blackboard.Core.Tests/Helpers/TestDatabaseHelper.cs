using Blackboard.Core.Configuration;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Blackboard.Core.Tests.Helpers;

public class TestDatabaseHelper : IDisposable
{
    private readonly SqliteConnection _connection;
    public DatabaseManager DatabaseManager { get; }

    public TestDatabaseHelper()
    {
        // Create in-memory SQLite database for testing
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var config = new TestDatabaseConfiguration
        {
            ConnectionString = _connection.ConnectionString,
            EnableWalMode = false,
            ConnectionTimeoutSeconds = 30,
            EnableBackup = false,
            BackupPath = ""
        };

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        DatabaseManager = new DatabaseManager(logger, config);
    }

    public async Task InitializeAsync()
    {
        await DatabaseManager.InitializeAsync();
    }

    public void Dispose()
    {
        DatabaseManager?.Dispose();
        _connection?.Dispose();
    }

    private class TestDatabaseConfiguration : IDatabaseConfiguration
    {
        public string ConnectionString { get; set; } = "";
        public bool EnableWalMode { get; set; }
        public int ConnectionTimeoutSeconds { get; set; }
        public bool EnableBackup { get; set; }
        public string BackupPath { get; set; } = "";
    }
}
