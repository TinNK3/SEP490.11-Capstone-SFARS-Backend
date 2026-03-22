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

            ApplySearchFilter(spec, specParams.Search);

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
                spec.ApplyPaging(specParams.GetTake(), specParams.GetSkip());
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

            ApplySearchFilter(spec, specParams.Search);

            if (specParams.Type.HasValue)
                spec.AddFilter(f => f.Type == specParams.Type.Value);

            if (specParams.IsActive.HasValue)
                spec.AddFilter(f => f.IsActive == specParams.IsActive.Value);

            if (specParams.HasAntivenom.HasValue)
                spec.AddFilter(f => f.HasAntivenom == specParams.HasAntivenom.Value);

            return spec;
        }

        private static void ApplySearchFilter(MedicalFacilitySpecification spec, string? rawSearch)
        {
            var search = rawSearch?.Trim();
            if (string.IsNullOrEmpty(search))
                return;

            var normalized = search.ToLower();
            spec.AddFilter(f =>
                f.Name.ToLower().Contains(normalized)
                || (f.Address != null && f.Address.ToLower().Contains(normalized)));
        }
    }
}