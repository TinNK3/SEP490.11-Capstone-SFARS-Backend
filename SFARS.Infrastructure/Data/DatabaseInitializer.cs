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

            // [Snakes] - Master data for snake species (Sync Data)
            await SeedSnakesAsync();

            // [FirstAidDetails] - First aid steps by toxin group (Sync Data)
            await SeedFirstAidDetailsAsync();
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
                    IsOnline = false,
                    Address = "25 Trần Phú, TP. Pleiku, Gia Lai",
                    CurrentLocation = new NetTopologySuite.Geometries.Point(108.0089, 13.9833) { SRID = 4326 }, // TP. Pleiku
                    LocationUpdatedAt = DateTime.UtcNow,
                    LocationAccuracyMeters = 15.0
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
                    IsOnline = false,
                    Address = "10 Lê Lợi, TP. Pleiku, Gia Lai",
                    CurrentLocation = new NetTopologySuite.Geometries.Point(108.0200, 13.9900) { SRID = 4326 }, // Gần TP. Pleiku
                    LocationUpdatedAt = DateTime.UtcNow,
                    LocationAccuracyMeters = 10.0
                }, rescuerRole)
            };

            var seededCount = 0;
            foreach (var (user, role) in usersToSeed)
            {
                // Check if user already exists by email
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    // Update location if seed has location but existing user doesn't
                    if (user.CurrentLocation != null && existingUser.CurrentLocation == null)
                    {
                        existingUser.CurrentLocation = user.CurrentLocation;
                        existingUser.LocationUpdatedAt = user.LocationUpdatedAt ?? DateTime.UtcNow;
                        existingUser.LocationAccuracyMeters = user.LocationAccuracyMeters;
                        existingUser.Address ??= user.Address;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated location for existing user {Email}.", user.Email);
                    }
                    else
                    {
                        _logger.LogInformation("User {Email} already exists, skipping.", user.Email);
                    }
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

        //  Summary:
        //      Seeding Snake Master Data (Upsert by ScientificName)
        private async Task SeedSnakesAsync()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "snakes_seed.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("[Seeding] Snakes seed file NOT found at: {FilePath}", filePath);
                return;
            }

            try
            {
                var jsonData = await File.ReadAllTextAsync(filePath);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var seedSnakes = System.Text.Json.JsonSerializer.Deserialize<List<Snake>>(jsonData, options);

                if (seedSnakes == null || !seedSnakes.Any())
                {
                    _logger.LogWarning("[Seeding] Snake list is empty or null after deserialization.");
                    return;
                }

                // 1. Get existing snakes keyed by ScientificName (natural key)
                var existingSnakes = await _context.Snakes
                    .ToDictionaryAsync(s => s.ScientificName, s => s);

                var newSnakes = new List<Snake>();
                var updatedCount = 0;
                var now = DateTime.UtcNow;

                // 2. Iterate and compare
                foreach (var seed in seedSnakes)
                {
                    if (existingSnakes.TryGetValue(seed.ScientificName, out var existing))
                    {
                        // Update if content changed
                        bool isChanged = false;
                        if (existing.CommonName != seed.CommonName) { existing.CommonName = seed.CommonName; isChanged = true; }
                        if (existing.ToxicityLevel != seed.ToxicityLevel) { existing.ToxicityLevel = seed.ToxicityLevel; isChanged = true; }
                        if (existing.ToxinGroup != seed.ToxinGroup) { existing.ToxinGroup = seed.ToxinGroup; isChanged = true; }
                        if (existing.Description != seed.Description) { existing.Description = seed.Description; isChanged = true; }
                        if (existing.KeyIdentifiers != seed.KeyIdentifiers) { existing.KeyIdentifiers = seed.KeyIdentifiers; isChanged = true; }
                        if (existing.TypicalSymptoms != seed.TypicalSymptoms) { existing.TypicalSymptoms = seed.TypicalSymptoms; isChanged = true; }
                        if (existing.Habitat != seed.Habitat) { existing.Habitat = seed.Habitat; isChanged = true; }
                        if (existing.DistributionNote != seed.DistributionNote) { existing.DistributionNote = seed.DistributionNote; isChanged = true; }
                        if (existing.Note != seed.Note) { existing.Note = seed.Note; isChanged = true; }

                        if (isChanged)
                        {
                            existing.UpdatedAt = now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // New snake
                        seed.Id = Guid.NewGuid();
                        seed.CreatedAt = now;
                        seed.IsActive = true;
                        newSnakes.Add(seed);
                    }
                }

                // 3. Batch save
                if (newSnakes.Any())
                {
                    await _context.Snakes.AddRangeAsync(newSnakes);
                }

                if (newSnakes.Any() || updatedCount > 0)
                {
                    var rows = await _context.SaveChangesAsync();
                    _logger.LogInformation("[Seeding] Snakes sync completed. Inserted: {Inserted}, Updated: {Updated}, DB rows: {Rows}.",
                        newSnakes.Count, updatedCount, rows);
                }
                else
                {
                    _logger.LogInformation("[Seeding] Snakes are up-to-date. No changes needed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seeding] Error seeding snakes.");
                throw;
            }
        }

        //  Summary:
        //      Seeding First Aid Details by ToxinGroup (Upsert by ToxinGroup + StepOrder + LanguageCode)
        //      ToxinGroup.Unknown = General prohibitions ("Không nên làm") — always returned with any result
        private async Task SeedFirstAidDetailsAsync()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "first_aid_seed.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("[Seeding] FirstAid seed file NOT found at: {FilePath}", filePath);
                return;
            }

            try
            {
                var jsonData = await File.ReadAllTextAsync(filePath);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var seedItems = System.Text.Json.JsonSerializer.Deserialize<List<FirstAidDetail>>(jsonData, options);

                if (seedItems == null || !seedItems.Any())
                {
                    _logger.LogWarning("[Seeding] FirstAid list is empty or null after deserialization.");
                    return;
                }

                // 1. Get existing records keyed by composite key (ToxinGroup, StepOrder, LanguageCode)
                var existingItems = await _context.FirstAidDetails
                    .Where(f => f.SnakeId == null) // Only general (non-snake-specific) records
                    .ToListAsync();

                var existingLookup = existingItems
                    .ToDictionary(
                        f => (f.ToxinGroup, f.StepOrder, f.LanguageCode),
                        f => f);

                var newItems = new List<FirstAidDetail>();
                var updatedCount = 0;
                var now = DateTime.UtcNow;

                // 2. Iterate and compare
                foreach (var seed in seedItems)
                {
                    var key = (seed.ToxinGroup, seed.StepOrder, seed.LanguageCode);

                    if (existingLookup.TryGetValue(key, out var existing))
                    {
                        // Update if content changed
                        bool isChanged = false;
                        if (existing.Title != seed.Title) { existing.Title = seed.Title; isChanged = true; }
                        if (existing.ContentMarkdown != seed.ContentMarkdown) { existing.ContentMarkdown = seed.ContentMarkdown; isChanged = true; }

                        if (isChanged)
                        {
                            existing.UpdatedAt = now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // New record
                        seed.Id = Guid.NewGuid();
                        seed.SnakeId = null; // General (not snake-specific)
                        seed.CreatedAt = now;
                        newItems.Add(seed);
                    }
                }

                // 3. Batch save
                if (newItems.Any())
                {
                    await _context.FirstAidDetails.AddRangeAsync(newItems);
                }

                if (newItems.Any() || updatedCount > 0)
                {
                    var rows = await _context.SaveChangesAsync();
                    _logger.LogInformation("[Seeding] FirstAidDetails sync completed. Inserted: {Inserted}, Updated: {Updated}, DB rows: {Rows}.",
                        newItems.Count, updatedCount, rows);
                }
                else
                {
                    _logger.LogInformation("[Seeding] FirstAidDetails are up-to-date. No changes needed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seeding] Error seeding FirstAidDetails.");
                throw;
            }
        }
    }
}