namespace SFARS.Application.Dtos;

public class CommentPaginatedResultDto<T> : PaginatedResultDto<T>
{
    public int TotalComments { get; set; }

    public CommentPaginatedResultDto(IEnumerable<T> items, int page, int pageSize, int totalPages, int totalItems, int totalComments)
        : base(items, page, pageSize, totalPages, totalItems)
    {
        TotalComments = totalComments;
    }
}
