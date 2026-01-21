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

    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Snake> Snakes { get; set; }
    public DbSet<SystemMessage> SystemMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}