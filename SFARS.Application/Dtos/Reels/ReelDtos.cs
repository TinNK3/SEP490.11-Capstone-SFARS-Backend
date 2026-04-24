using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SFARS.Application.Dtos.Reels;



public class ReelAuthorDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}

public class ReelResponseDto
{
    public Guid Id { get; set; }
    public ReelAuthorDto Author { get; set; } = null!;
    
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
    public ReelAuthorDto Author { get; set; } = null!;
    
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    
    public Guid? ParentId { get; set; }
    public int TotalReplies { get; set; }
}
