using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using System.Text.Json.Serialization;

namespace SFARS.Application.Dtos.User
{
    public class UserDto
    {
        public Guid Id { get; set; }
        
        // Identity Info
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FullName => $"{FirstName} {LastName}";
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }

        // Profile
        public string? Address { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? Dob { get; set; }

        // Status
        public UserStatus Status { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActiveAt { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Security - internal use only (not serialized to response)
        [JsonIgnore]
        public string? Password { get; set; }

        [JsonIgnore]
        public string? PasswordHash { get; set; }

        [JsonIgnore]
        public string? EmailVerificationCode { get; set; }

        [JsonIgnore]
        public bool TwoFactorEnabled { get; set; }

        // Role info
        public string? Role { get; set; }

        // Internal flags for admin PUT /admin/users/{id} orchestration
        [JsonIgnore]
        public bool HasStatusUpdate { get; set; }

        [JsonIgnore]
        public bool HasRoleUpdate { get; set; }
    }

    public static class UserDtoExtensions
    {
        public static AuthUserDto ToAuthUserDto(this UserDto userDto)
        {
            var role = userDto.Role ?? UserTypeConstants.User;

            return new AuthUserDto
            {
                Id = userDto.Id,
                Email = userDto.Email,
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Phone = userDto.Phone,
                PasswordHash = userDto.PasswordHash,
                EmailVerificationCode = userDto.EmailVerificationCode,
                Avatar = userDto.Avatar,
                Address = userDto.Address,
                Gender = userDto.Gender,
                Dob = userDto.Dob,
                Status = userDto.Status,
                IsOnline = userDto.IsOnline,
                LastActiveAt = userDto.LastActiveAt,
                CreatedAt = userDto.CreatedAt,
                UpdatedAt = userDto.UpdatedAt,
                RoleName = role,
                IsRescuer = role == UserTypeConstants.Rescuer,
            };
        }
    }
}
