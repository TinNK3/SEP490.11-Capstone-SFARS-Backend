using System.ComponentModel;

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