using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying Snake entities
    /// </summary>
    public class SnakeSpecification : BaseSpecification<Snake>
    {
        public SnakeSpecification(SnakeSpecParams specParams, bool isCount = false) : base()
        {
            // Apply Search
            if (!string.IsNullOrEmpty(specParams.Search))
            {
                var normalized = specParams.Search.Trim().ToLower();
                AddFilter(s => s.CommonName.ToLower().Contains(normalized) 
                            || s.ScientificName.ToLower().Contains(normalized));
            }

            // Apply Filters
            if (specParams.ToxicityLevel.HasValue)
            {
                AddFilter(s => s.ToxicityLevel == specParams.ToxicityLevel.Value);
            }

            if (specParams.ToxinGroup.HasValue)
            {
                AddFilter(s => s.ToxinGroup == specParams.ToxinGroup.Value);
            }

            if (specParams.IsActive.HasValue)
            {
                AddFilter(s => s.IsActive == specParams.IsActive.Value);
            }

            // Skip Pagination and Sorting if this is just a Count query
            if (!isCount)
            {
                // Apply Sorting
                if (!string.IsNullOrEmpty(specParams.Sort))
                {
                    switch (specParams.Sort)
                    {
                        case "CommonNameAsc":
                            AddOrderBy(s => s.CommonName);
                            break;
                        case "CommonNameDesc":
                            AddOrderByDescending(s => s.CommonName);
                            break;
                        case "ScientificNameAsc":
                            AddOrderBy(s => s.ScientificName);
                            break;
                        case "ScientificNameDesc":
                            AddOrderByDescending(s => s.ScientificName);
                            break;
                        default:
                            AddOrderBy(s => s.CommonName);
                            break;
                    }
                }
                else
                {
                    AddOrderBy(s => s.CommonName);
                }

                // Apply Pagination
                ApplyPaging(specParams.GetTake(), specParams.GetSkip());
            }
        }

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
    }
}