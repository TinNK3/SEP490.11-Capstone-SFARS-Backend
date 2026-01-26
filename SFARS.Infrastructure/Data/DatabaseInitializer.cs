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

            // [SystemMessages] - For result codes/messages
            if (!await _context.SystemMessages.AnyAsync()) 
                await SeedSystemMessagesAsync();
            else 
                _logger.LogInformation("Already seeded data for table {Table}", "SystemMessage");
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
        //      Seeding System Messages (for result codes)
        private async Task SeedSystemMessagesAsync()
        {
            var messages = new List<SystemMessage>
            {
                // Success messages
                new() { MsgId = "SYS.Success0001", MsgContent = "Created successfully", En = "Created successfully", Vi = "Tạo thành công" },
                new() { MsgId = "SYS.Success0002", MsgContent = "Retrieved successfully", En = "Retrieved successfully", Vi = "Lấy dữ liệu thành công" },
                new() { MsgId = "SYS.Success0003", MsgContent = "Updated successfully", En = "Updated successfully", Vi = "Cập nhật thành công" },
                new() { MsgId = "SYS.Success0004", MsgContent = "Deleted successfully", En = "Deleted successfully", Vi = "Xóa thành công" },
                
                // Warning messages
                new() { MsgId = "SYS.Warning0002", MsgContent = "Not found {0}", En = "Not found {0}", Vi = "Không tìm thấy {0}" },
                new() { MsgId = "SYS.Warning0004", MsgContent = "No data found", En = "No data found", Vi = "Không có dữ liệu" },
                
                // Auth messages
                new() { MsgId = "Auth.Success0002", MsgContent = "Sign in successfully", En = "Sign in successfully", Vi = "Đăng nhập thành công" },
                new() { MsgId = "Auth.Warning0001", MsgContent = "Account is inactive", En = "Account is inactive", Vi = "Tài khoản không hoạt động" },
                new() { MsgId = "Auth.Warning0007", MsgContent = "Invalid password", En = "Invalid password", Vi = "Mật khẩu không đúng" },
                new() { MsgId = "Auth.Warning0010", MsgContent = "MFA verification required", En = "MFA verification required", Vi = "Yêu cầu xác thực 2 lớp" },
                
                // Failure messages
                new() { MsgId = "SYS.Fail0001", MsgContent = "Failed to create {0}", En = "Failed to create {0}", Vi = "Tạo {0} thất bại" },
                new() { MsgId = "SYS.Fail0003", MsgContent = "Failed to update {0}", En = "Failed to update {0}", Vi = "Cập nhật {0} thất bại" }
            };

            await _context.SystemMessages.AddRangeAsync(messages);
            var saved = await _context.SaveChangesAsync() > 0;

            if (saved)
                _logger.LogInformation("Seeded {Count} system messages successfully.", messages.Count);
        }
    }
}