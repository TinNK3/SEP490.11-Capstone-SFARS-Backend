using SFARS.Application.Common;
using System;
using System.Collections.Generic;

namespace SFARS.Application.Dtos.Community;

public record UnifiedContentDto(
    Guid Id,
    string Type, // "Post" | "Reel"
    PostAuthorDto Author,
    string? Content,
    List<string> MediaUrls,
    int LikeCount,
    int CommentCount,
    bool IsLikedByMe,
    DateTime CreatedAt
);

public record UnifiedContentListResponse(
    List<UnifiedContentDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
