using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum SnakeRiskLevel
    {
        [Description("Không độc")]
        NonVenomous,
        [Description("Độc nhẹ")]
        MildlyVenomous,
        [Description("Độc cao")]
        HighlyVenomous,
        [Description("Cực độc")]
        Deadly
    }
}