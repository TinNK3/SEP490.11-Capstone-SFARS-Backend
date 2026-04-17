using System.ComponentModel;

namespace SFARS.Domain.Common.Enum;

/// <summary>
/// Professional auditing filters for incident reviews.
/// Helps admins categorize incidents by their media status and review progress.
/// </summary>
public enum IncidentAuditFilter
{
    [Description("Tất cả")]
    All = 0,

    [Description("Bỏ qua chụp ảnh")]
    NoMedia = 1,

    [Description("Chưa ai đánh giá")]
    NotReviewed = 2,

    [Description("Chờ Admin duyệt (Rescuer đã đánh giá)")]
    RescuerReviewed = 3,

    [Description("Đã xác minh (Admin đã chốt)")]
    Verified = 4
}