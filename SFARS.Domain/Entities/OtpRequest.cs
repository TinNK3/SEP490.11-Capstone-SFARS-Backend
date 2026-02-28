using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class OtpRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = null!;
    public OtpType Type { get; set; }
    public DateTime ExpiredAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public int AttemptCount { get; set; } = 0;

    // Navigation Properties
    public virtual User User { get; set; } = null!;
}
