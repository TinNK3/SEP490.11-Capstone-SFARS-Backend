namespace SFARS.Domain.Specifications.Params
{
    /// <summary>
    /// Base class for specification parameters.
    /// </summary>
    public class BaseSpecParams
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        public int? Page { get; set; } = DefaultPage;
        public int? PageSize { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; }

        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }

        public int GetTake()
        {
            var pageSize = PageSize.GetValueOrDefault(DefaultPageSize);
            if (pageSize <= 0) return DefaultPageSize;
            return pageSize > MaxPageSize ? MaxPageSize : pageSize;
        }

        public int GetPage()
        {
            var page = Page.GetValueOrDefault(DefaultPage);
            return page < DefaultPage ? DefaultPage : page;
        }

        public int GetSkip()
        {
            return (GetPage() - 1) * GetTake();
        }
    }
}