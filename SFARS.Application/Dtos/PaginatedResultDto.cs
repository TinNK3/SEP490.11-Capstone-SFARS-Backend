namespace SFARS.Application.Dtos;

public class PaginatedResultDto<T>
{
    public IEnumerable<T> Items { get; set; }
    public PaginationInfoDto Pagination { get; set; }

    public PaginatedResultDto(IEnumerable<T> items, int page, int pageSize, int totalPages, int totalItems)
    {
        Items = items;
        Pagination = new PaginationInfoDto(page, pageSize, totalItems, totalPages);
    }
}