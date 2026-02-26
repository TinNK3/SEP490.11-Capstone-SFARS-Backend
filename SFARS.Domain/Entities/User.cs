using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class User : BaseEntity
{
    // Identity Info
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = null!;
    public string? Phone { get; set; } 
    public string PasswordHash { get; set; } = null!;
    public string? Avatar { get; set; }

    // Medical / Profile
    public string? Address { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? Dob { get; set; }

    // Status & System
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool IsOnline { get; set; }
    public DateTime? LastActiveAt { get; set; }

    // Location (Real-time)
    public Point? CurrentLocation { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
    public double? LocationAccuracyMeters { get; set; }

    // Email Verification
    public string? EmailVerificationCode { get; set; }

    // Navigation Properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserDevice> UserDevices { get; set; } = new List<UserDevice>();
    public virtual ICollection<UserLoginHistory> LoginHistories { get; set; } = new List<UserLoginHistory>();
    public virtual RescuerProfile? RescuerProfile { get; set; }
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual UserPoint? UserPoint { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}