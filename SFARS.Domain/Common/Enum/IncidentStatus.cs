using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum IncidentStatus
    {
        [Description("Chờ xử lý")] 
        Pending,
        [Description("Đã phân công")] 
        Assigned,
        [Description("Đang tới")] 
        EnRoute,
        [Description("Đã đến nơi")] 
        Arrived,
        [Description("Đã xác minh")] 
        Verified,
        [Description("Bàn giao cơ sở y tế")] 
        Handover,
        [Description("Đã đóng ca")] 
        Closed,
        [Description("Đã huỷ")] 
        Cancelled
    }
}