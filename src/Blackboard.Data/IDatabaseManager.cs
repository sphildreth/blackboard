using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Blackboard.Data;

public interface IDatabaseManager
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
    Task<T> QueryFirstAsync<T>(string sql, object? param = null);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null);
    Task<int> ExecuteAsync(string sql, object? param = null);
    Task InitializeAsync();
    Task<int> ExecuteNonQueryAsync(string sql, params SqliteParameter[] parameters);
    Task<object?> ExecuteScalarAsync(string sql, params SqliteParameter[] parameters);
    Task<SqliteDataReader> ExecuteReaderAsync(string sql, params SqliteParameter[] parameters);
    Task BackupDatabaseAsync(string backupPath);
    Task CloseAsync();
}
