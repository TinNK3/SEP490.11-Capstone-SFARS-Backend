using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces;
using SFARS.Infrastructure.Data.Context;
using SFARS.Domain.Entities;
using SFARS.Domain.Common.Enum;
using System.Linq;

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
                _logger.LogInformation("Applying migrations...");
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database initialized successfully (migrations applied).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw; // Re-throw so startup stops on migration error
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

            // [GeminiApiKeys] - Migrate config keys to DB
            await SeedGeminiApiKeysAsync();

            // [SystemMessages] - For result codes/messages (Sync Data)
            await SeedSystemMessagesAsync();

            // [Snakes] - Master data for snake species (Sync Data)
            await SeedSnakesAsync();

            // [FirstAidDetails] - First aid steps by toxin group (Sync Data)
            await SeedFirstAidDetailsAsync();

            // [Faqs] - Frequently asked questions (Sync Data)
            await SeedFaqsAsync();

            // [Analytics Demo] - Demo incidents/missions/transactions for dashboard preview
            await SeedAnalyticsDemoDataAsync();
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
                    Email = "[EMAIL_ADDRESS]",
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

                var seedMessagesRaw = System.Text.Json.JsonSerializer.Deserialize<List<SystemMessage>>(jsonData, options);

                if (seedMessagesRaw == null || !seedMessagesRaw.Any())
                {
                    _logger.LogWarning("[Seeding] Message list is empty or null after deserialization.");
                    return;
                }

                // Ensure MsgId is distinct to avoid unique constraint violations
                var seedMessages = seedMessagesRaw.DistinctBy(m => m.MsgId).ToList();

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

                var seedSnakesRaw = System.Text.Json.JsonSerializer.Deserialize<List<Snake>>(jsonData, options);

                if (seedSnakesRaw == null || !seedSnakesRaw.Any())
                {
                    _logger.LogWarning("[Seeding] Snake list is empty or null after deserialization.");
                    return;
                }

                // Ensure ScientificName is distinct
                var seedSnakes = seedSnakesRaw.DistinctBy(s => s.ScientificName).ToList();

                // 1. Get existing snakes keyed by ScientificName (natural key)
                var existingSnakes = await _context.Snakes
                    .Include(s => s.SnakeImages)
                    .ToDictionaryAsync(s => s.ScientificName, s => s);

                var newSnakes = new List<Snake>();
                var updatedCount = 0;
                var now = DateTime.UtcNow;
                bool hasNewImages = false;

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

                        // Check and insert images if they are completely missing
                        if (seed.SnakeImages != null && seed.SnakeImages.Any() && !existing.SnakeImages.Any())
                        {
                            foreach (var seedImg in seed.SnakeImages)
                            {
                                var newImg = new SnakeImage
                                {
                                    Id = Guid.NewGuid(),
                                    SnakeId = existing.Id,
                                    ImageUrl = seedImg.ImageUrl,
                                    IsPrimary = seedImg.IsPrimary,
                                    CreatedAt = now
                                };
                                await _context.SnakeImages.AddAsync(newImg);
                            }
                            hasNewImages = true;
                        }

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

                if (newSnakes.Any() || updatedCount > 0 || hasNewImages)
                {
                    var rows = await _context.SaveChangesAsync();
                    _logger.LogInformation("[Seeding] Snakes sync completed. Inserted/Updated: {Rows}.", rows);
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

                var seedItemsRaw = System.Text.Json.JsonSerializer.Deserialize<List<FirstAidDetail>>(jsonData, options);

                if (seedItemsRaw == null || !seedItemsRaw.Any())
                {
                    _logger.LogWarning("[Seeding] FirstAid list is empty or null after deserialization.");
                    return;
                }

                // Ensure uniqueness by composite key (ToxinGroup + LanguageCode + StepOrder + SnakeId)
                var seedItems = seedItemsRaw.DistinctBy(f => new { f.ToxinGroup, f.LanguageCode, f.StepOrder, f.SnakeId }).ToList();

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

        //  Summary:
        //      Seeding FAQ Data (Upsert by Question text)
        private async Task SeedFaqsAsync()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "faq_seed.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("[Seeding] FAQ seed file NOT found at: {FilePath}", filePath);
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

                var seedFaqs = System.Text.Json.JsonSerializer.Deserialize<List<Faq>>(jsonData, options);

                if (seedFaqs == null || !seedFaqs.Any())
                {
                    _logger.LogWarning("[Seeding] FAQ list is empty or null after deserialization.");
                    return;
                }

                // 1. Get existing FAQs keyed by Question (natural key)
                var existingFaqs = await _context.Faqs
                    .ToDictionaryAsync(f => f.Question, f => f);

                var newFaqs = new List<Faq>();
                var updatedCount = 0;
                var now = DateTime.UtcNow;

                // 2. Iterate and compare
                foreach (var seed in seedFaqs)
                {
                    if (existingFaqs.TryGetValue(seed.Question, out var existing))
                    {
                        // Update if content changed
                        bool isChanged = false;
                        if (existing.Answer != seed.Answer) { existing.Answer = seed.Answer; isChanged = true; }
                        if (existing.Order != seed.Order) { existing.Order = seed.Order; isChanged = true; }
                        if (existing.IsActive != seed.IsActive) { existing.IsActive = seed.IsActive; isChanged = true; }

                        if (isChanged)
                        {
                            existing.UpdatedAt = now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // New FAQ
                        seed.Id = Guid.NewGuid();
                        seed.CreatedAt = now;
                        newFaqs.Add(seed);
                    }
                }

                // 3. Batch save
                if (newFaqs.Any())
                {
                    await _context.Faqs.AddRangeAsync(newFaqs);
                }

                if (newFaqs.Any() || updatedCount > 0)
                {
                    var rows = await _context.SaveChangesAsync();
                    _logger.LogInformation("[Seeding] FAQs sync completed. Inserted: {Inserted}, Updated: {Updated}, DB rows: {Rows}.",
                        newFaqs.Count, updatedCount, rows);
                }
                else
                {
                    _logger.LogInformation("[Seeding] FAQs are up-to-date. No changes needed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seeding] Error seeding FAQs.");
                throw;
            }
        }

        //  Summary:
        //      Seeding demo analytics data for dashboard preview.
        //      Guard: skips if any Incident seeded by this method already exists (checked via Code prefix "DEMO-").
        private async Task SeedAnalyticsDemoDataAsync()
        {
            if (await _context.Incidents.AnyAsync(i => i.Code.StartsWith("DEMO-")))
            {
                _logger.LogInformation("[Seeding] Analytics demo data already exists, skipping.");
                return;
            }

            _logger.LogInformation("[Seeding] Seeding analytics demo data...");

            var now = DateTime.UtcNow;
            var passwordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Test123!");

            // --- 1. Ensure demo users exist (2 victims + 3 rescuers) ---
            var demoEmails = new[]
            {
                "victim1@demo.sfars", "victim2@demo.sfars",
                "rescuer1@demo.sfars", "rescuer2@demo.sfars", "rescuer3@demo.sfars"
            };

            var userRole    = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            var rescuerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Rescuer");
            if (userRole == null || rescuerRole == null)
            {
                _logger.LogError("[Seeding] Roles not found. Skipping analytics demo data.");
                return;
            }

            // Demo user definitions: (firstName, lastName, email, role, lat, lng)
            var demoUserDefs = new (string First, string Last, string Email, Role Role, double Lat, double Lng)[]
            {
                ("An",     "Nguyễn",  "victim1@demo.sfars",  userRole,    10.7769, 106.7009), // HCM
                ("Bình",   "Trần",    "victim2@demo.sfars",  userRole,    16.0471, 108.2068), // Đà Nẵng
                ("Cường",  "Lê",      "rescuer1@demo.sfars", rescuerRole, 10.7820, 106.6980), // HCM
                ("Dũng",   "Phạm",    "rescuer2@demo.sfars", rescuerRole, 16.0600, 108.2100), // Đà Nẵng
                ("Hải",    "Võ",      "rescuer3@demo.sfars", rescuerRole, 10.7850, 106.7050), // HCM
            };

            var demoUsers = new List<User>();
            foreach (var def in demoUserDefs)
            {
                var existing = await _context.Users.FirstOrDefaultAsync(u => u.Email == def.Email);
                if (existing != null) { demoUsers.Add(existing); continue; }

                var u = new User
                {
                    Id           = Guid.NewGuid(),
                    FirstName    = def.First,
                    LastName     = def.Last,
                    Email        = def.Email,
                    PasswordHash = passwordHash,
                    Status       = UserStatus.Active,
                    CurrentLocation = new NetTopologySuite.Geometries.Point(def.Lng, def.Lat) { SRID = 4326 },
                    LocationUpdatedAt = now,
                    CreatedAt    = now
                };
                await _context.Users.AddAsync(u);
                await _context.SaveChangesAsync();

                await _context.UserRoles.AddAsync(new UserRole { UserId = u.Id, RoleId = def.Role.Id, AssignedAt = now });
                await _context.SaveChangesAsync();

                // Seed RescuerProfile for rescuers (UserId is the shared PK)
                if (def.Role.RoleName == "Rescuer")
                {
                    await _context.RescuerProfiles.AddAsync(new RescuerProfile
                    {
                        UserId         = u.Id,
                        IsVerified     = true,
                        IsAvailable    = true,
                        CoverageRadiusKM = 30,
                        VehicleType    = VehicleType.Motorbike
                    });
                    await _context.SaveChangesAsync();
                }

                demoUsers.Add(u);
            }

            var victims   = demoUsers.Where(u => demoUserDefs.Any(d => d.Email == u.Email && d.Role.RoleName == "User")).ToList();
            var rescuers  = demoUsers.Where(u => demoUserDefs.Any(d => d.Email == u.Email && d.Role.RoleName == "Rescuer")).ToList();

            // --- 2. Pick snake species from DB ---
            var snakes = await _context.Snakes.Take(3).ToListAsync();
            if (!snakes.Any())
            {
                _logger.LogWarning("[Seeding] No snakes in DB — AI inference demo data will be skipped.");
            }

            // --- 3. Seed 12 Incidents spread over the last 60 days ---
            // Locations: various sites across Vietnam
            var incidentDefs = new[]
            {
                // (daysAgo, severityLevel, status, closed, lat, lng)
                (60, SeverityLevel.High,   IncidentStatus.Closed,     true,  10.7769, 106.7009),  // HCM
                (55, SeverityLevel.Medium, IncidentStatus.Closed,     true,  16.0471, 108.2068),  // Đà Nẵng
                (50, SeverityLevel.Low,    IncidentStatus.Closed,     true,  21.0278, 105.8342),  // Hà Nội
                (45, SeverityLevel.High,   IncidentStatus.Closed,     true,  10.9700, 106.8450),  // Biên Hoà
                (40, SeverityLevel.Medium, IncidentStatus.Closed,     true,  10.3500, 107.0800),  // Bà Rịa
                (35, SeverityLevel.High,   IncidentStatus.Closed,     true,  16.4700, 107.6000),  // Huế
                (30, SeverityLevel.Low,    IncidentStatus.Closed,     true,  10.0452, 105.7469),  // Cần Thơ
                (20, SeverityLevel.Medium, IncidentStatus.Closed,     true,  10.7769, 106.6950),  // HCM
                (15, SeverityLevel.High,   IncidentStatus.Assigned,   false, 10.8000, 106.7100),  // HCM (active)
                (10, SeverityLevel.Medium, IncidentStatus.Assigned,   false, 16.0500, 108.2200),  // Đà Nẵng (active)
                (5,  SeverityLevel.Low,    IncidentStatus.Pending,    false, 21.0300, 105.8400),  // Hà Nội (active)
                (1,  SeverityLevel.High,   IncidentStatus.Arrived,    false, 10.9800, 106.8500),  // Biên Hoà (active)
            };

            var incidents = new List<Incident>();
            for (int i = 0; i < incidentDefs.Length; i++)
            {
                var def    = incidentDefs[i];
                var victim = victims[i % victims.Count];
                var snake  = snakes.Count > 0 ? snakes[i % snakes.Count] : null;
                var createdAt = now.AddDays(-def.Item1).AddHours(-7); // store as UTC (ICT -7h)

                var incident = new Incident
                {
                    Id            = Guid.NewGuid(),
                    Code          = $"DEMO-{now.Year}-{(i + 1):D3}",
                    VictimId      = victim.Id,
                    SnakeId       = snake?.Id,
                    Location      = new NetTopologySuite.Geometries.Point(def.Item6, def.Item5) { SRID = 4326 },
                    AddressString = $"Demo Address {i + 1}, Vietnam",
                    CurrentStatus = def.Item3,
                    PriorityLevel = def.Item2,
                    CreatedAt     = createdAt
                };
                incidents.Add(incident);
            }

            await _context.Incidents.AddRangeAsync(incidents);
            await _context.SaveChangesAsync();

            // --- 4. Seed RescueMissions for closed incidents ---
            var missionDefs = incidentDefs
                .Select((def, idx) => (def, incident: incidents[idx]))
                .Where(x => x.def.Item4) // only closed incidents
                .ToList();

            for (int i = 0; i < missionDefs.Count; i++)
            {
                var (def, incident) = missionDefs[i];
                var rescuer   = rescuers[i % rescuers.Count];
                var createdAt = incident.CreatedAt.AddMinutes(5);
                var startedAt = createdAt.AddMinutes(2);
                var arrivedAt = startedAt.AddMinutes(15 + (i * 3));
                var completedAt = arrivedAt.AddMinutes(30);

                await _context.RescueMissions.AddAsync(new RescueMission
                {
                    Id          = Guid.NewGuid(),
                    IncidentId  = incident.Id,
                    RescuerId   = rescuer.Id,
                    Status      = RescueStatus.Completed,
                    StartedAt   = startedAt,
                    ArrivedAt   = arrivedAt,
                    CompletedAt = completedAt,
                    CreatedAt   = createdAt
                });
            }

            // Seed active missions for open incidents
            var activeMissions = incidentDefs
                .Select((def, idx) => (def, incident: incidents[idx]))
                .Where(x => !x.def.Item4)
                .ToList();

            for (int i = 0; i < activeMissions.Count; i++)
            {
                var (def, incident) = activeMissions[i];
                var rescuer   = rescuers[i % rescuers.Count];
                var createdAt = incident.CreatedAt.AddMinutes(5);

                await _context.RescueMissions.AddAsync(new RescueMission
                {
                    Id         = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    RescuerId  = rescuer.Id,
                    Status     = RescueStatus.Accepted,
                    StartedAt  = createdAt,
                    CreatedAt  = createdAt
                });
            }

            await _context.SaveChangesAsync();

            // --- 5. Seed AiInference records (one per incident that has a snake) ---
            if (snakes.Any())
            {
                foreach (var incident in incidents.Where(i => i.SnakeId.HasValue))
                {
                    var snake = snakes.First(s => s.Id == incident.SnakeId);
                    await _context.AiInferences.AddAsync(new AiInference
                    {
                        Id               = Guid.NewGuid(),
                        IncidentId       = incident.Id,
                        ModelName        = "snake-cls-demo",
                        ModelVersion     = "1.0.0",
                        SelectedSnakeId  = snake.Id,
                        SelectedConfidence = 0.75 + (new Random().NextDouble() * 0.20), // 0.75–0.95
                        SelectedToxinGroup = snake.ToxinGroup,
                        TopK             = 3,
                        CreatedAt        = incident.CreatedAt.AddMinutes(1)
                    });
                }
                await _context.SaveChangesAsync();
            }

            // --- 6. Seed paid Transactions (donations) ---
            var donationAmounts = new[] { 50000m, 100000m, 200000m, 150000m, 75000m, 500000m, 300000m, 250000m };
            for (int i = 0; i < donationAmounts.Length; i++)
            {
                var payer = demoUsers[i % demoUsers.Count];
                var txDate = now.AddDays(-(i * 7));
                await _context.Transactions.AddAsync(new Transaction
                {
                    Id              = Guid.NewGuid(),
                    UserId          = payer.Id,
                    Amount          = donationAmounts[i],
                    Status          = PaymentStatus.Paid,
                    Description     = $"Ủng hộ quỹ cứu hộ rắn cắn #{i + 1}",
                    TransactionDate = txDate,
                    CreatedAt       = txDate
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("[Seeding] Analytics demo data seeded: {Incidents} incidents, {Missions} missions, {Txns} donations.",
                incidents.Count, missionDefs.Count + activeMissions.Count, donationAmounts.Length);
        }

        private async Task SeedGeminiApiKeysAsync()
        {
            if (await _context.GeminiApiKeys.AnyAsync()) return;

            var defaultKeys = new[]
            {
                "AIzaSyCEbn67X__3Lbv-93nFz_l9Ex4auoQd4EY",
                "AIzaSyCgWRGJXisSYZhHklzjDKwE1aGvBvTVhHU",
                "AIzaSyBmX6xj6hHh8NDA-tiauFLmIKrEiyJCwqA",
                "AIzaSyAmiGcTjJ_ZblIyFqTM3HOMkecu6Y_7tjU",
                "AIzaSyBKr0_t4QQhPLgsHPFOzYqDC8EXumM7Igs",
                "AIzaSyD8l5zobi8GgVi77IUhITfCwsTAoTtitcA",
                "AIzaSyBvq_J5AyTW6MATgqySKKRmuUxRM9wG554",
                "AIzaSyBeAgNG5lByj-VvkcG9j8ne4y5y0DG9YR0",
                "AIzaSyDie-gFbCNvIaoabbnbVOaD-7e0JDxnD-k",
                "AIzaSyDvV-y3BsmGHgFrWNjhfy9e41D6Ip7FHx0"
            };

            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@sfars.com" || u.Email == "[EMAIL_ADDRESS]");
            var adminId = adminUser?.Id;

            for (int i = 0; i < defaultKeys.Length; i++)
            {
                await _context.GeminiApiKeys.AddAsync(new GeminiApiKey
                {
                    Id = Guid.NewGuid(),
                    KeyValue = defaultKeys[i],
                    Label = $"Migrated Key #{i + 1}",
                    IsActive = true,
                    IsExhausted = false,
                    TotalUsageCount = 0,
                    ConsecutiveFailures = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = adminId
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("[Seeding] Migrated {Count} Gemini API Keys to DB.", defaultKeys.Length);
        }

    }
}
