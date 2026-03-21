using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    public class UserSpecParams : BaseSpecParams
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Gender? Gender { get; set; }
        public UserStatus? Status { get; set; }
        public string? Role { get; set; }
        public DateTime? DobFrom { get; set; }
        public DateTime? DobTo { get; set; }
        public DateTime? ModifiedFrom { get; set; }
        public DateTime? ModifiedTo { get; set; }
    }
}