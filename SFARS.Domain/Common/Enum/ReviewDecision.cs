using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum ReviewDecision
    {
        [Description("Xác nhận kết quả AI")]
        Approved,

        [Description("Bác bỏ, ghi đè kết quả AI")]
        Rejected
    }
}