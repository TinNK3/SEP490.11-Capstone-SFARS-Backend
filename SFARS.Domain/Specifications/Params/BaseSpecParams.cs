namespace SFARS.Domain.Specifications.Params
{
    /// <summary>
    /// Base class for specification parameters.
    /// </summary>
    public class BaseSpecParams
    {
        private const int DefaultPage = 1;
        private const int DefaultLimit = 10;
        private const int MaxPageSize = 100;

        public int? Page { get; set; } = DefaultPage;
        public int? Limit { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; }

        public int PageNumber
        {
            get
            {
                var page = Page.GetValueOrDefault(DefaultPage);
                return page < DefaultPage ? DefaultPage : page;
            }
        }

        public int PageSize
        {
            get
            {
                var limit = Limit.GetValueOrDefault(DefaultLimit);
                if (limit <= 0) return DefaultLimit;
                return limit > MaxPageSize ? MaxPageSize : limit;
            }
        }

        public int PageIndex => PageNumber - 1;
    }
}