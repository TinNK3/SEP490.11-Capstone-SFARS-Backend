using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum NotificationType
    {
        [Description("Hệ thống")]
        System = 0,
        [Description("Cứu hộ")]
        Mission = 1,
        [Description("Cảnh báo")]
        Alert = 2,
        [Description("Điều phối SOS")]
        SosDispatch = 3,
        [Description("Bài viết cộng đồng")]
        CommunityPost = 4,
        [Description("Reel")]
        Reel = 5,
        [Description("Thích")]
        Like = 6,
        [Description("Bình luận")]
        Comment = 7,
        [Description("Phản hồi bình luận")]
        CommentReply = 8,
        [Description("Chia sẻ")]
        Share = 9
    }
}