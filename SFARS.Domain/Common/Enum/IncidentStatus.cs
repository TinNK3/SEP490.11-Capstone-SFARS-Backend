using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum IncidentStatus
    {
        [Description("Chờ xử lý")]
        Pending,

        // Tiered Dispatching (0s – 90s)
        [Description("Đang điều phối vòng 1")]   Dispatching_Tier1,
        [Description("Đang điều phối vòng 2")]   Dispatching_Tier2,
        [Description("Đang điều phối vòng 3")]   Dispatching_Tier3,

        // Post-Dispatch
        [Description("Chưa tìm được cứu hộ")]    Unassigned,   // All tiers failed — kept alive
        [Description("Đã phân công")]            Assigned,
        [Description("Đang tới")]                EnRoute,
        [Description("Đã đến nơi")]              Arrived,
        [Description("Bàn giao cơ sở y tế")]     Handover,
        [Description("Đã đóng ca")]              Closed,
        [Description("Đã huỷ")]                  Cancelled
    }
}