using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum RetrainStatus
    {
        [Description("Đang khởi tạo")]
        Pending = 0,

        [Description("Đang xuất dữ liệu vàng (Dataset)")]
        ExportingData = 1,

        [Description("Đang huấn luyện (MLOps Pipeline)")]
        Training = 2,

        [Description("Huấn luyện thành công")]
        Success = 3,

        [Description("Huấn luyện thất bại")]
        Failed = 4
    }
}