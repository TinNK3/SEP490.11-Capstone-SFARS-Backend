using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

/// <summary>
/// Nhật ký hành động của Admin.
/// Ghi lại mỗi thao tác Admin thực hiện trên một entity (ví dụ: User).
/// </summary>
public class AdminAuditLog : BaseEntity
{
    /// <summary>Admin thực hiện hành động.</summary>
    public Guid AdminId { get; set; }

    /// <summary>Hành động (CreateUser, UpdateUserStatus, …).</summary>
    public AdminAction Action { get; set; }

    /// <summary>Loại đối tượng bị tác động (e.g. "User").</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>ID đối tượng bị tác động.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Giá trị trước thay đổi (JSON).</summary>
    public string? OldValue { get; set; }

    /// <summary>Giá trị sau thay đổi (JSON).</summary>
    public string? NewValue { get; set; }

    /// <summary>Lý do (nếu có, ví dụ: lý do ban user).</summary>
    public string? Reason { get; set; }

    /// <summary>Địa chỉ IP của Admin tại thời điểm thao tác.</summary>
    public string? IpAddress { get; set; }

    // Navigation
    public virtual User Admin { get; set; } = null!;
}
