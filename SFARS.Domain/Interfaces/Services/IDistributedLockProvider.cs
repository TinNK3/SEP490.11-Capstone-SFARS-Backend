namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Provides mechanism for distributed locking to ensure only one node 
/// can process a specific resource at a time.
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Try to acquire a lock for a specific resource.
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock.</param>
    /// <param name="timeout">How long to wait for the lock to become available.</param>
    /// <returns>A lock handle if successful, otherwise null.</returns>
    Task<IDistributedLock?> TryAcquireLockAsync(string lockKey, TimeSpan timeout);
}

/// <summary>
/// Represents a handle to an acquired distributed lock.
/// Should be disposed to release the lock.
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
}