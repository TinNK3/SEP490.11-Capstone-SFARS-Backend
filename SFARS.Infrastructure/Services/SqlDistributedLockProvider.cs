using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces.Services;
using SFARS.Infrastructure.Data.Context;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Implementation of IDistributedLockProvider using SQL Server Application Locks (sp_getapplock).
/// This is a lightweight distributed lock provider that utilizes the existing database.
/// </summary>
public class SqlDistributedLockProvider : IDistributedLockProvider
{
    private readonly SFARSDbContext _context;
    private readonly ILogger<SqlDistributedLockProvider> _logger;

    public SqlDistributedLockProvider(SFARSDbContext context, ILogger<SqlDistributedLockProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IDistributedLock?> TryAcquireLockAsync(string lockKey, TimeSpan timeout)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // sp_getapplock @Resource = 'key', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = timeout_ms
        var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;

        var resourceParam = new SqlParameter("@Resource", lockKey);
        var lockModeParam = new SqlParameter("@LockMode", "Exclusive");
        var lockOwnerParam = new SqlParameter("@LockOwner", "Session");
        var lockTimeoutParam = new SqlParameter("@LockTimeout", (int)timeout.TotalMilliseconds);
        var returnValue = new SqlParameter("@ReturnValue", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };

        command.Parameters.Add(resourceParam);
        command.Parameters.Add(lockModeParam);
        command.Parameters.Add(lockOwnerParam);
        command.Parameters.Add(lockTimeoutParam);
        command.Parameters.Add(returnValue);

        try
        {
            await command.ExecuteNonQueryAsync();
            int result = (int)returnValue.Value;

            if (result >= 0) // 0: success, 1: success after wait
            {
                return new SqlDistributedLock(connection, lockKey, _logger);
            }

            _logger.LogWarning("SqlDistributedLockProvider: Failed to acquire lock '{LockKey}'. Result code: {Result}", lockKey, result);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlDistributedLockProvider: Exception while acquiring lock '{LockKey}'", lockKey);
            return null;
        }
    }

    private class SqlDistributedLock : IDistributedLock
    {
        private readonly DbConnection _connection;
        private readonly string _lockKey;
        private readonly ILogger _logger;
        private bool _disposed;

        public SqlDistributedLock(DbConnection connection, string lockKey, ILogger logger)
        {
            _connection = connection;
            _lockKey = lockKey;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    var command = _connection.CreateCommand();
                    command.CommandText = "sp_releaseapplock";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@Resource", _lockKey));
                    command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SqlDistributedLock: Error releasing lock '{LockKey}'", _lockKey);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}