namespace SFARS.Application.Dtos;

public class PaginatedResultDto<T>
{
    public IEnumerable<T> Sources { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPage { get; set; }
    public int TotalActualItem { get; set; }

    public PaginatedResultDto(IEnumerable<T> sources, int page, int limit, int totalPage, int totalActualItem)
    {
        Sources = sources;
        Page = page;
        Limit = limit;
        TotalPage = totalPage;
        TotalActualItem = totalActualItem;
    }
}