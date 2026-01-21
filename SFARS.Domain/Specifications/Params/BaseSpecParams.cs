namespace SFARS.Domain.Specifications.Params
{
    /// <summary>
    /// Base class for specification parameters.
    /// </summary>
    public class BaseSpecParams
    {
        public int? PageIndex { get; set; } = 1;
        public int? PageSize { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; }
    }
}