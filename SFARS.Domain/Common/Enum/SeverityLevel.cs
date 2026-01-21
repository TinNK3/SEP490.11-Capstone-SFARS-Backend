using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Domain.Common.Enum
{
    public enum SeverityLevel
    {
        [Description("Không rõ")] 
        Unknown,
        [Description("Thấp")] 
        Low,
        [Description("Trung bình")] 
        Medium,
        [Description("Cao")] 
        High,
        [Description("Nguy kịch")] 
        Critical
    }
}