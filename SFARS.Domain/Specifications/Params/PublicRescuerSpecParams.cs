using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Specifications.Params
{
    public class PublicRescuerSpecParams : BaseSpecParams
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Gender? Gender { get; set; }
    }
}