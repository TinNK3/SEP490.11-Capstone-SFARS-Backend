using SFARS.Domain.Interfaces.Repositories.Base;

namespace SFARS.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        // Generic repository
        IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class;

        // Use for simple inserts, updates, or deletions
        int SaveChanges();
        Task<int> SaveChangesAsync();

        // Use for operations when requiring data consistency accross multiple tables
        // or when handling complex logic with dependencies
        int SaveChangesWithTransaction();
        Task<int> SaveChangesWithTransactionAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
        void ClearTracking();

        // SQL Sequence support for thread-safe code generation
        Task<long> GetNextSequenceValueAsync(string sequenceName);

        // Raw SQL Execution
        Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters);
    }
}
