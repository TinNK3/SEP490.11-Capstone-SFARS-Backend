using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using System.Reflection;

namespace SFARS.Infrastructure.Data.Context;

public partial class SFARSDbContext : DbContext
{
    public SFARSDbContext(DbContextOptions<SFARSDbContext> options)
        : base(options)
    {
    }

    // IAM
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<OtpRequest> OtpRequests { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserDevice> UserDevices { get; set; }
    public DbSet<UserLoginHistory> UserLoginHistories { get; set; }
    public DbSet<RescuerProfile> RescuerProfiles { get; set; }

    // Snake & Medical
    public DbSet<Snake> Snakes { get; set; }
    public DbSet<SnakeImage> SnakeImages { get; set; }
    public DbSet<SnakeChangeLog> SnakeChangeLogs { get; set; }
    public DbSet<FirstAidDetail> FirstAidDetails { get; set; }
    public DbSet<MedicalFacility> MedicalFacilities { get; set; }

    // Operations
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<IncidentStatusHistory> IncidentStatusHistories { get; set; }
    public DbSet<IncidentMedia> IncidentMedias { get; set; }
    public DbSet<RescueMission> RescueMissions { get; set; }
    public DbSet<RescueTrackingLog> RescueTrackingLogs { get; set; }
    public DbSet<Review> Reviews { get; set; }

    // Community
    public DbSet<ContentPost> ContentPosts { get; set; }
    public DbSet<PostMedia> PostMedias { get; set; }
    public DbSet<PostComment> PostComments { get; set; }
    public DbSet<PostLike> PostLikes { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<UserPoint> UserPoints { get; set; }
    public DbSet<PointTransaction> PointTransactions { get; set; }
    public DbSet<SnakeHotspot> SnakeHotspots { get; set; }

    // Finance & System
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<SystemConfig> SystemConfigs { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }
    public DbSet<SystemMessage> SystemMessages { get; set; }

    // Admin
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }
    public DbSet<Faq> Faqs { get; set; }

    public DbSet<AiInference> AiInferences { get; set; }
    public DbSet<AiInferenceCandidate> AiInferenceCandidates { get; set; }
    public DbSet<IncidentSymptom> IncidentSymptoms { get; set; }

    // Chat (Incident-specific)
    public DbSet<IncidentChat> IncidentChats { get; set; }
    public DbSet<IncidentChatMessage> IncidentChatMessages { get; set; }
    public DbSet<IncidentChatMessageMedia> IncidentChatMessageMedias { get; set; }

    // Chat (General — RAG-based AI chatbox)
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}