using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    public class UserSpecParams : BaseSpecParams
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Gender? Gender { get; set; }
        public UserStatus? Status { get; set; }
        public DateTime?[]? DobRange { get; set; }
        public DateTime?[]? CreateDateRange { get; set; }
        public DateTime?[]? ModifiedDateRange { get; set; }
    }
}