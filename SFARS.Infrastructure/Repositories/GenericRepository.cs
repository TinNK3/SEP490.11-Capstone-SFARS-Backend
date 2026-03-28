using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Infrastructure.Data;
using SFARS.Infrastructure.Data.Context;
using System.Diagnostics;
using System.Linq.Expressions;

namespace SFARS.Infrastructure.Repositories
{
    public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity, TKey>
        where TEntity : class
    {
        private readonly SFARSDbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public GenericRepository(SFARSDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }
        #region READ DATA

        public async Task<TEntity?> GetByIdAsync(TKey id)
        {
            return await _dbSet.FindAsync(id);
        }

        public IQueryable<TEntity> GetQueryable(bool tracked = true)
        {
            return tracked ? _dbSet : _dbSet.AsNoTracking();
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync(bool tracked = true)
        {
            IQueryable<TEntity> query = _dbSet;

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            return await query.ToListAsync();
        }

        public async Task<TEntity?> GetWithSpecAsync(ISpecification<TEntity> specification, bool tracked = true)
        {
            // Apply specification to retrieve data
            return tracked
                ? await ApplySpecification(specification).FirstOrDefaultAsync()
                : await ApplySpecification(specification).AsNoTracking().FirstOrDefaultAsync();
        }

        public async Task<TResult?> GetWithSpecAndSelectorAsync<TResult>(ISpecification<TEntity> specification,
            Expression<Func<TEntity, TResult>> selector, bool tracked = true)
        {
            // Apply specification to retrieve data
            return tracked
                 ? await ApplySpecification(specification).Select(selector).FirstOrDefaultAsync()
                 : await ApplySpecification(specification).AsNoTracking().Select(selector).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<TEntity>> GetAllWithSpecAsync(ISpecification<TEntity> specification, bool tracked = true)
        {
            IQueryable<TEntity> query = _dbSet;

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            return await ApplySpecification(query, specification).ToListAsync();
        }

        public async Task<IEnumerable<TResult>> GetAllWithSpecAndSelectorAsync<TResult>(ISpecification<TEntity> specification,
            Expression<Func<TEntity, TResult>> selector,
            bool tracked = true)
        {
            IQueryable<TEntity> query = _dbSet;

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            // Apply specification
            query = ApplySpecification(query, specification);

            // Apply projection
            return await query.Select(selector).ToListAsync();
        }

        public async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public async Task<int> CountAsync(ISpecification<TEntity> specification)
        {
            return await ApplySpecification(specification).CountAsync();
        }

        public async Task<int> SumAsync(Expression<Func<TEntity, int>> predicate)
        {
            return await _dbSet.SumAsync(predicate);
        }

        public async Task<int> SumWithSpecAsync(ISpecification<TEntity> specification, Expression<Func<TEntity, int>> predicate)
        {
            return await ApplySpecification(specification).SumAsync(predicate);
        }

        public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> expression)
        {
            return await _dbSet.AllAsync(expression);
        }

        public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression)
        {
            return await _dbSet.AnyAsync(expression);
        }

        public async Task<bool> AnyAsync(ISpecification<TEntity> specification)
        {
            return await ApplySpecification(specification).AnyAsync();
        }

        #endregion

        #region WRITE DATA
        public void Add(TEntity entity)
        {
            _dbSet.Add(entity);
        }

        public void AddRange(IEnumerable<TEntity> entities)
        {
            _dbSet.AddRange(entities);
        }

        public void Update(TEntity entity)
        {
            _dbSet.Attach(entity);
            _dbSet.Entry(entity).State = EntityState.Modified;
        }

        public async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        /// <summary>
        /// Deletes an entity by ID using EF Core 7+ ExecuteDeleteAsync.
        /// Executes DELETE directly in database without loading entity into memory.
        /// </summary>
        /// <returns>Number of rows affected (0 if not found, 1 if deleted)</returns>
        public async Task<int> DeleteAsync(TKey id)
        {
            // Get primary key property name
            var primaryKeyProperty = _context.Model.FindEntityType(typeof(TEntity))
                ?.FindPrimaryKey()?.Properties
                .Select(p => p.Name)
                .FirstOrDefault() ?? "Id";

            // EF Core 7+ ExecuteDeleteAsync - direct DELETE in database
            return await _dbSet
                .Where(e => EF.Property<TKey>(e, primaryKeyProperty)!.Equals(id))
                .ExecuteDeleteAsync();
        }

        /// <summary>
        /// Deletes multiple entities by their IDs using a single DELETE query.
        /// EF Core 7+ ExecuteDeleteAsync - no entities loaded into memory.
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public async Task<int> DeleteRangeAsync(TKey[] ids)
        {
            if (ids == null || ids.Length == 0)
                return 0;

            // Get primary key property name
            var primaryKeyProperty = _context.Model.FindEntityType(typeof(TEntity))
                ?.FindPrimaryKey()?.Properties
                .Select(p => p.Name)
                .FirstOrDefault() ?? "Id";

            // Single DELETE query for all IDs
            return await _dbSet
                .Where(e => ids.Contains(EF.Property<TKey>(e, primaryKeyProperty)))
                .ExecuteDeleteAsync();
        }

        /// <summary>
        /// Deletes entities matching a specification using ExecuteDeleteAsync.
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public async Task<int> DeleteWithSpecAsync(ISpecification<TEntity> specification)
        {
            return await ApplySpecification(specification).ExecuteDeleteAsync();
        }

        public Task UpdateAsync(TEntity entity)
        {
            _dbSet.Attach(entity);
            _dbSet.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates entities matching a specification using ExecuteUpdateAsync.
        /// </summary>
        /// <returns>Number of rows affected</returns>
        public async Task<int> UpdateWithSpecAsync(
            ISpecification<TEntity> specification,
            Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<TEntity>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<TEntity>>> setPropertyCalls)
        {
            return await ApplySpecification(specification).ExecuteUpdateAsync(setPropertyCalls);
        }

        #endregion

        #region OTHERS
        public bool HasChanges(TEntity original, TEntity modified)
        {
            if (original == null || modified == null)
            {
                throw new ArgumentNullException("Entities cannot be null.");
            }

            // Compare each property in the entity
            var properties = typeof(TEntity).GetProperties();
            foreach (var property in properties)
            {
                // Ignore properties without "get" or "set" accessors
                if (!property.CanRead || !property.CanWrite) continue;

                var originalValue = property.GetValue(original);
                var modifiedValue = property.GetValue(modified);

                // Check if the values are different
                if (!Equals(originalValue, modifiedValue))
                {
                    return true; // A difference is found
                }
            }

            return false; // No differences found
        }

        public bool HasChanges(TEntity entity)
        {
            var entityEntry = _dbSet.Entry(entity);

            // Check if the entity is in the modified state
            if (entityEntry.State == EntityState.Modified)
            {
                return true;
            }

            // Get the properties of the entity, excluding the primary key
            var primaryKeyProperty = _context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()?.Properties
                 .Select(p => p.Name)
                 .FirstOrDefault();

            foreach (var property in entityEntry.OriginalValues.Properties)
            {
                // Skip the primary key property
                if (property.Name == primaryKeyProperty)
                {
                    continue;
                }

                var originalValue = entityEntry.OriginalValues[property];
                var currentValue = entityEntry.CurrentValues[property];

                // If the original value is different from current value, mark as change
                if (!object.Equals(originalValue, currentValue))
                {
                    return true;
                }
            }

            return false; // No changes 
        }
        #endregion

        // Apply defined specification for retrieving data with conditions
        private IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification)
        {
            return SpecificationEvaluator<TEntity>.GetQuery(_dbSet.AsQueryable(), specification);
        }

        private IQueryable<TEntity> ApplySpecification(IQueryable<TEntity> query, ISpecification<TEntity> specification)
        {
            return SpecificationEvaluator<TEntity>.GetQuery(query, specification);
        }
    }
}
