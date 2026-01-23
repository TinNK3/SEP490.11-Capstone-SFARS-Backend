using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum RescueStatus
    {
        [Description("Chờ phản hồi")] 
        Pending,
        [Description("Đã nhận")] 
        Accepted,
        [Description("Từ chối")] 
        Rejected,
        [Description("Đã phân công lại")] 
        Reassigned,
        [Description("Hoàn tất")] 
        Completed
    }
}