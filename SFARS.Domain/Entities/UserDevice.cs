using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class UserDevice : BaseEntity
{
    public Guid UserId { get; set; }
    public string DeviceToken { get; set; } = null!;
    public DevicePlatform Platform { get; set; }
    public DateTime LastLoginAt { get; set; }

    public virtual User User { get; set; } = null!;
}