using SFARS.Domain.Entities;
using System.Linq.Expressions;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying Snake entities
    /// </summary>
    public class SnakeSpecification : BaseSpecification<Snake>
    {
        /// <summary>
        /// Default constructor for getting all snakes
        /// </summary>
        public SnakeSpecification() : base()
        {
            AddOrderBy(s => s.Name);
        }

        /// <summary>
        /// Get snake by ID
        /// </summary>
        public SnakeSpecification(int snakeId) : base(s => s.SnakeId == snakeId)
        {
        }

        /// <summary>
        /// Search snakes by name (contains)
        /// </summary>
        public static SnakeSpecification ByNameContains(string searchTerm)
        {
            var spec = new SnakeSpecification();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                spec.AddFilter(s => s.Name.Contains(searchTerm));
            }
            spec.AddOrderBy(s => s.Name);
            return spec;
        }

        /// <summary>
        /// Get snakes with pagination
        /// </summary>
        public static SnakeSpecification WithPagination(int pageIndex, int pageSize)
        {
            var spec = new SnakeSpecification();
            spec.ApplyPaging(pageSize, pageIndex * pageSize);
            spec.AddOrderBy(s => s.Name);
            return spec;
        }

        /// <summary>
        /// Search with pagination
        /// </summary>
        public static SnakeSpecification SearchWithPagination(string? searchTerm, int pageIndex, int pageSize)
        {
            var spec = new SnakeSpecification();
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                spec.AddFilter(s => s.Name.Contains(searchTerm));
            }
            
            spec.ApplyPaging(pageSize, pageIndex * pageSize);
            spec.AddOrderBy(s => s.Name);
            
            return spec;
        }
    }
}
