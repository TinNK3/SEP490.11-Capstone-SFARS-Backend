using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum UnableToAssessReason
    {
        [Description("Ảnh mờ, chất lượng kém")]
        ImageBlur = 0,

        [Description("Không thấy rắn trong ảnh")]
        NoSnakeSeen = 1,

        [Description("Lý do khác")]
        Other = 2,

        [Description("Triệu chứng không rõ ràng")]
        SymptomsInsufficient = 3
    }
}