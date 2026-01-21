using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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