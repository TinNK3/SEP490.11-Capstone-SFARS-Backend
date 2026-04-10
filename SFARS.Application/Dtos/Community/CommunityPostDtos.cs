namespace SFARS.Application.Dtos.Community;

// ── REQUEST ─────────────────────────────────────────────────────────────

public record PostMediaUploadDto(System.IO.Stream Stream, string FileName, string ContentType);

public record CreateCommentRequest(
    string Content,
    Guid? ParentId
);

// ── RESPONSE ────────────────────────────────────────────────────────────

public record PostAuthorDto(Guid Id, string FullName, string? AvatarUrl);

public record PostMediaDto(Guid Id, string Url, string ContentType, int Order);

public record PostCommentDto(
    Guid Id,
    Guid PostId,
    PostAuthorDto Author,
    string Content,
    Guid? ParentId,
    DateTime CreatedAt,
    int TotalReplies
);

public record CommunityPostDto(
    Guid Id,
    PostAuthorDto Author,
    string? Content,
    List<PostMediaDto> Medias,
    int LikeCount,
    int CommentCount,
    bool IsLikedByMe,
    DateTime CreatedAt,
    CommunityPostDto? SharedPost = null,
    SFARS.Application.Dtos.Reels.ReelResponseDto? SharedReel = null
);

public record PostListResponse(
    List<CommunityPostDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── SIGNALR PAYLOADS ────────────────────────────────────────────────────

public record NewPostPayload(
    Guid PostId,
    PostAuthorDto Author,
    string? ContentPreview,
    int MediaCount,
    DateTime CreatedAt
);

public record NewCommentPayload(
    Guid CommentId,
    Guid PostId,
    PostAuthorDto Author,
    string Content,
    Guid? ParentId,
    DateTime CreatedAt
);

public record LikeUpdatedPayload(Guid PostId, int NewLikeCount, bool IsLiked);
