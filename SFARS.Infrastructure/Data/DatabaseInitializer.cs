using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces;
using SFARS.Infrastructure.Data.Context;
using SFARS.Domain.Entities;
using SFARS.Domain.Common.Enum;

namespace SFARS.Infrastructure.Data
{
    //	Summary:
    //		This class is to initialize database and seeding default data for the application
    public class DatabaseInitializer : IDatabaseInitializer
    {
        
        private readonly SFARSDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(ILogger<DatabaseInitializer> logger,
            SFARSDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        //  Summary:
        //      Try to initialize database and its table if not exist
        public async Task InitializeAsync()
        {
            try
            {
                if (!await _context.Database.CanConnectAsync())
                {
                    _logger.LogWarning("Database cannot be connected to.");
                    return;
                }

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Database initialized successfully (migrations applied).");
                }
                else
                {
                    _logger.LogInformation("Database is up to date. No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        //  Summary:
        //      Perform seeding data - ORDER MATTERS (dependencies first)
        public async Task TrySeedAsync()
        {
            // [Roles] - Seed missing roles
            await SeedRolesAsync();

            // [Users] - Seed missing users (check by email)
            await SeedUsersAsync();

            // [SystemMessages] - For result codes/messages (Sync Data)
            await SeedSystemMessagesAsync();
        }

        //  Summary:
        //      Seeding Roles - Seeds only missing roles
        private async Task SeedRolesAsync()
        {
            var rolesToSeed = new List<(string Name, string Description)>
            {
                ("Admin", "System administrator with full access"),
                ("User", "Standard user"),
                ("Rescuer", "Certified rescuer profile")
            };

            var seededCount = 0;
            foreach (var (name, description) in rolesToSeed)
            {
                var existingRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == name);
                if (existingRole != null)
                {
                    _logger.LogInformation("Role {RoleName} already exists, skipping.", name);
                    continue;
                }

                await _context.Roles.AddAsync(new Role { RoleName = name, Description = description });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded role {RoleName}.", name);
                seededCount++;
            }

            if (seededCount > 0)
                _logger.LogInformation("Seeded {Count} new roles successfully.", seededCount);
        }

        //  Summary:
        //      Seeding Users (Test/Admin accounts) - Seeds only missing users
        private async Task SeedUsersAsync()
        {
            // Get roles for assignment
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            var rescuerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Rescuer");

            if (adminRole == null || userRole == null || rescuerRole == null)
            {
                _logger.LogError("Required roles not found. Cannot seed users.");
                return;
            }

            // Password: Test123! (BCrypt hashed)
            var passwordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");

            // Define users to seed with their roles
            var usersToSeed = new List<(User User, Role Role)>
            {
                (new User
                {
                    FirstName = "Admin",
                    LastName = "System",
                    Email = "admin@sfars.com",
                    Phone = "0901234567",
                    PasswordHash = passwordHash,
                    Gender = Gender.Male,
                    Status = UserStatus.Active,
                    IsOnline = false
                }, adminRole),
                (new User
                {
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com",
                    Phone = "0909876543",
                    PasswordHash = passwordHash,
                    Gender = Gender.Male,
                    Status = UserStatus.Active,
                    IsOnline = false
                }, userRole),
                (new User
                {
                    FirstName = "Rescue",
                    LastName = "Team",
                    Email = "rescuer@sfars.com",
                    Phone = "0912345678",
                    PasswordHash = passwordHash,
                    Gender = Gender.Male,
                    Status = UserStatus.Active,
                    IsOnline = false
                }, rescuerRole)
            };

            var seededCount = 0;
            foreach (var (user, role) in usersToSeed)
            {
                // Check if user already exists by email
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    _logger.LogInformation("User {Email} already exists, skipping.", user.Email);
                    continue;
                }

                // Add user
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                // Assign role
                await _context.UserRoles.AddAsync(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded user {Email} with role {Role}.", user.Email, role.RoleName);
                seededCount++;
            }

            if (seededCount > 0)
                _logger.LogInformation("Seeded {Count} new users successfully.", seededCount);
            else
                _logger.LogInformation("No new users to seed.");
        }

        //  Summary:
        //      Seeding System Messages (for result codes) with Upsert Logic
        private async Task SeedSystemMessagesAsync()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "system_messages.json");
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"[Seeding] Seed file NOT found at: {filePath}");
                return;
            }

            try
            {
                var jsonData = await File.ReadAllTextAsync(filePath);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                };

                var seedMessages = System.Text.Json.JsonSerializer.Deserialize<List<SystemMessage>>(jsonData, options);

                if (seedMessages == null || !seedMessages.Any())
                {
                    _logger.LogWarning("[Seeding] Message list is empty or null after deserialization.");
                    return;
                }

                // 1. Get existing messages Keyed by MsgId (Batch Query)
                var existingMessages = await _context.SystemMessages
                    .ToDictionaryAsync(m => m.MsgId, m => m);

                var newMessages = new List<SystemMessage>();
                var updatedCount = 0;
                var now = new DateTime(2026, 1, 29);

                // 2. Iterate and Compare
                foreach (var seedMsg in seedMessages)
                {
                    if (existingMessages.TryGetValue(seedMsg.MsgId, out var existingMsg))
                    {
                        // Update if content changed (Data Integrity)
                        bool isChanged = false;
                        if (existingMsg.MsgContent != seedMsg.MsgContent) { existingMsg.MsgContent = seedMsg.MsgContent; isChanged = true; }
                        if (existingMsg.En != seedMsg.En) { existingMsg.En = seedMsg.En; isChanged = true; }
                        if (existingMsg.Vi != seedMsg.Vi) { existingMsg.Vi = seedMsg.Vi; isChanged = true; }

                        if (isChanged)
                        {
                            existingMsg.UpdatedAt = DateTime.UtcNow;
                            // Explicitly track modification if needed, though EF Core tracks changes automatically on attached entities
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // New message
                        seedMsg.Id = Guid.NewGuid();
                        seedMsg.CreatedAt = now;
                        newMessages.Add(seedMsg);
                    }
                }

                // 3. Batch Save
                if (newMessages.Any())
                {
                    await _context.SystemMessages.AddRangeAsync(newMessages);
                }

                if (newMessages.Any() || updatedCount > 0)
                {
                    var rows = await _context.SaveChangesAsync();
                    _logger.LogInformation($"[Seeding] Sync completed. Inserted: {newMessages.Count}, Updated: {updatedCount}, DB Rows affected: {rows}.");
                }
                else
                {
                    _logger.LogInformation("[Seeding] SystemMessages are up-to-date. No changes needed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seeding] Error during deserialization or syncing.");
                throw;
            }
        }
    }
}