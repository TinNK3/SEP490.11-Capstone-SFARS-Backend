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
            AddOrderBy(s => s.CommonName);
        }

        /// <summary>
        /// Get snake by ID
        /// </summary>
        public SnakeSpecification(Guid id) : base(s => s.Id == id)
        {
        }

        /// <summary>
        /// Search snakes by name (contains CommonName or ScientificName)
        /// </summary>
        public static SnakeSpecification ByNameContains(string searchTerm)
        {
            var spec = new SnakeSpecification();
            ApplySearchFilter(spec, searchTerm);
            spec.AddOrderBy(s => s.CommonName);
            return spec;
        }

        /// <summary>
        /// Get snakes with pagination
        /// </summary>
        public static SnakeSpecification WithPagination(int pageIndex, int pageSize)
        {
            var spec = new SnakeSpecification();
            spec.ApplyPaging(pageSize, pageIndex * pageSize);
            spec.AddOrderBy(s => s.CommonName);
            return spec;
        }

        /// <summary>
        /// Search with pagination
        /// </summary>
        public static SnakeSpecification SearchWithPagination(string? searchTerm, int pageIndex, int pageSize)
        {
            var spec = new SnakeSpecification();

            ApplySearchFilter(spec, searchTerm);
            
            spec.ApplyPaging(pageSize, pageIndex * pageSize);
            spec.AddOrderBy(s => s.CommonName);
            
            return spec;
        }

        private static void ApplySearchFilter(SnakeSpecification spec, string? rawSearch)
        {
            var search = rawSearch?.Trim();
            if (string.IsNullOrEmpty(search))
                return;

            var normalized = search.ToLower();
            spec.AddFilter(s =>
                s.CommonName.ToLower().Contains(normalized)
                || s.ScientificName.ToLower().Contains(normalized));
        }
    }
}