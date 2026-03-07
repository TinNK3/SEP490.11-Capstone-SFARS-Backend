using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying MedicalFacility entities.
    /// </summary>
    public class MedicalFacilitySpecification : BaseSpecification<MedicalFacility>
    {
        /// <summary>
        /// Default constructor — order by Name.
        /// </summary>
        public MedicalFacilitySpecification() : base()
        {
            AddOrderBy(f => f.Name);
        }

        /// <summary>
        /// Get facility by ID.
        /// </summary>
        public MedicalFacilitySpecification(Guid id) : base(f => f.Id == id)
        {
        }

        /// <summary>
        /// Search and filter facilities with pagination.
        /// </summary>
        public static MedicalFacilitySpecification WithFilters(FacilitySpecParams specParams)
        {
            var spec = new MedicalFacilitySpecification();

            // Text search on Name or Address
            if (!string.IsNullOrWhiteSpace(specParams.Search))
            {
                spec.AddFilter(f => f.Name.Contains(specParams.Search)
                                 || (f.Address != null && f.Address.Contains(specParams.Search)));
            }

            // Filter by FacilityType
            if (specParams.Type.HasValue)
            {
                spec.AddFilter(f => f.Type == specParams.Type.Value);
            }

            // Filter by IsActive
            if (specParams.IsActive.HasValue)
            {
                spec.AddFilter(f => f.IsActive == specParams.IsActive.Value);
            }

            // Filter by HasAntivenom
            if (specParams.HasAntivenom.HasValue)
            {
                spec.AddFilter(f => f.HasAntivenom == specParams.HasAntivenom.Value);
            }

            // Pagination
            if (specParams.PageSize.HasValue)
            {
                var pageIndex = specParams.PageIndex ?? 1;
                spec.ApplyPaging(specParams.PageSize.Value, (pageIndex - 1) * specParams.PageSize.Value);
            }

            // Sorting
            if (!string.IsNullOrWhiteSpace(specParams.Sort))
            {
                var isDescending = specParams.Sort.StartsWith("-");
                var sortField = isDescending ? specParams.Sort[1..] : specParams.Sort;

                switch (sortField.ToLower())
                {
                    case "name":
                        if (isDescending) spec.AddOrderByDescending(f => f.Name);
                        else spec.AddOrderBy(f => f.Name);
                        break;
                    case "type":
                        if (isDescending) spec.AddOrderByDescending(f => f.Type);
                        else spec.AddOrderBy(f => f.Type);
                        break;
                    case "createdat":
                        if (isDescending) spec.AddOrderByDescending(f => f.CreatedAt);
                        else spec.AddOrderBy(f => f.CreatedAt);
                        break;
                    default:
                        spec.AddOrderBy(f => f.Name);
                        break;
                }
            }

            return spec;
        }

        /// <summary>
        /// Count spec — same filters but no pagination (for total count).
        /// </summary>
        public static MedicalFacilitySpecification CountSpec(FacilitySpecParams specParams)
        {
            var spec = new MedicalFacilitySpecification();

            if (!string.IsNullOrWhiteSpace(specParams.Search))
            {
                spec.AddFilter(f => f.Name.Contains(specParams.Search)
                                 || (f.Address != null && f.Address.Contains(specParams.Search)));
            }

            if (specParams.Type.HasValue)
                spec.AddFilter(f => f.Type == specParams.Type.Value);

            if (specParams.IsActive.HasValue)
                spec.AddFilter(f => f.IsActive == specParams.IsActive.Value);

            if (specParams.HasAntivenom.HasValue)
                spec.AddFilter(f => f.HasAntivenom == specParams.HasAntivenom.Value);

            return spec;
        }
    }
}
