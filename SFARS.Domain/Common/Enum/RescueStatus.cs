using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum RescueStatus
    {
        [Description("Đã nhận")] 
        Accepted,
        [Description("Từ chối")] 
        Rejected,
        [Description("Đã phân công lại")] 
        Reassigned,
        [Description("Đã đến nơi")]
        Arrived,
        [Description("Hoàn tất")] 
        Completed
    }
}