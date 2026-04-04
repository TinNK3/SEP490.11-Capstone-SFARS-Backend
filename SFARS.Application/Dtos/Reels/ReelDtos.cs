using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SFARS.Application.Dtos.Reels;


public class ReelResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public string? UserAvatar { get; set; }
    
    public string VideoUrl { get; set; } = null!;
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; }

    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    
    public bool IsLikedByCurrentUser { get; set; }
}


public class ReelCommentResponseDto
{
    public Guid Id { get; set; }
    public Guid ReelId { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public string? UserAvatar { get; set; }
    
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    
    public Guid? ParentCommentId { get; set; }
    public int SubCommentCount { get; set; }
}


public record ReelListResponse(List<ReelResponseDto> Items, int TotalCount, int PageNumber, int PageSize);

public record ReelCommentListResponse(List<ReelCommentResponseDto> Items, int TotalCount, int PageNumber, int PageSize);
