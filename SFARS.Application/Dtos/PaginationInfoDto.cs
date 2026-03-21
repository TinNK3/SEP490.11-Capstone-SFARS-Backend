namespace SFARS.Application.Dtos;

/// <summary>
/// Pagination metadata wrapped in a dedicated object
/// </summary>
public class PaginationInfoDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public PaginationInfoDto(int page, int pageSize, int totalItems, int totalPages)
    {
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = totalPages;
    }
}
