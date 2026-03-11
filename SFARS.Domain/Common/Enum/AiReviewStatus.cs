using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum AiReviewStatus
    {
        [Description("Chờ xử lý")]
        Pending = 0,

        [Description("Đánh giá sau")]
        Deferred = 1,

        [Description("AI nhận diện đúng")]
        ConfirmedCorrect = 2,

        [Description("Cập nhật lại kết quả AI")]
        Corrected = 3,

        [Description("Không thể đánh giá")]
        UnableToAssess = 4,

        [Description("Đã hủy (quá hạn)")]
        Abandoned = 5
    }
}