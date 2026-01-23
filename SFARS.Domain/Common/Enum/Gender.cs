using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum Gender
    {
        [Description("Nam")]
        Male,
        [Description("Nữ")]
        Female,
        [Description("Khác")]
        Other
    }
}