using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    /// <summary>
    /// Query parameters for filtering medical facilities.
    /// </summary>
    public class FacilitySpecParams : BaseSpecParams
    {
        public FacilityType? Type { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasAntivenom { get; set; }
    }
}