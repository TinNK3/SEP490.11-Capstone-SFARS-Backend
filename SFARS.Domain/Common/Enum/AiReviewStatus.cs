using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum AiReviewStatus
    {
        [Description("Chờ xử lý")]
        Pending = 0,

        [Description("AI nhận diện đúng")]
        ConfirmedCorrect = 1,

        [Description("Cập nhật lại kết quả AI")]
        Corrected = 2,

        [Description("Không thể đánh giá")]
        UnableToAssess = 3
    }
}