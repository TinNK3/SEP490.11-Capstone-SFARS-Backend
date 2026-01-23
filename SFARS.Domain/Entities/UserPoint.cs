using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Entities;

public class UserPoint
{
    [Key, ForeignKey("User")]
    public Guid UserId { get; set; }
    public int CurrentPoints { get; set; }
    public int LifetimePoints { get; set; }
    public UserRank CurrentRank { get; set; } = UserRank.Bronze;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}