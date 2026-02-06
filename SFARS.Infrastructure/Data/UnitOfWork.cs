using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Infrastructure.Data.Context;
using SFARS.Infrastructure.Repositories;
using System.Collections;

namespace SFARS.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SFARSDbContext _context;
        private Hashtable _repositories;

        public UnitOfWork(SFARSDbContext context)
        {
            _context = context;

            // Initialize repo hashtable if not exist
            if (_repositories == null) _repositories = new Hashtable();
        }

        public IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class
        {
            // Retrieves the name of the entity type
            var type = typeof(TEntity).Name;

            // Checks repository for particular entity is created
            if (!_repositories.ContainsKey(type))
            {
                // Defines the type of the generic repository
                var repositoryType = typeof(GenericRepository<TEntity, TKey>);

                // Creates an instance of the generic repository 
                var repositoryInstance = Activator.CreateInstance(repositoryType, _context);

                // Adds the created repository to the Hashtable
                _repositories.Add(type, repositoryInstance);
            }

            return (IGenericRepository<TEntity, TKey>)_repositories[type]!;
        }

        public int SaveChanges() => _context.SaveChanges();

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public int SaveChangesWithTransaction()
        {
            int result = -1;

            // Starts a new database transaction
            using (var dbContextTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // Saves all changes and commits the transaction if successful
                    result = _context.SaveChanges();
                    dbContextTransaction.Commit();
                }
                catch (Exception)
                {
                    // If an exception occurs, the transaction is rolled back
                    result = -1;
                    _context.Database.RollbackTransaction();
                    throw;
                }
            }

            return result;
        }

        public async Task<int> SaveChangesWithTransactionAsync()
        {
            int result = -1;

            // Starts a new database transaction
            using (var dbContextTransaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Saves all changes and commits the transaction if successful
                    result = await _context.SaveChangesAsync();
                    await dbContextTransaction.CommitAsync();
                }
                catch (Exception)
                {
                    // If an exception occurs, the transaction is rolled back
                    result = -1;
                    await _context.Database.RollbackTransactionAsync();
                    throw;
                }
            }

            return result;
        }

        /// <summary>
        /// Get next value from SQL SEQUENCE (thread-safe, atomic)
        /// </summary>
        public async Task<long> GetNextSequenceValueAsync(string sequenceName)
        {
            if (!SequenceNames.IsValid(sequenceName))
                throw new ArgumentException($"Invalid sequence name: {sequenceName}", nameof(sequenceName));

            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await _context.Database.OpenConnectionAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT NEXT VALUE FOR dbo.{sequenceName};";
                var result = await command.ExecuteScalarAsync();

                return Convert.ToInt64(result);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        public void Dispose() => _context.Dispose();

        public void ClearTracking() => _context.ChangeTracker.Clear();
    }
}