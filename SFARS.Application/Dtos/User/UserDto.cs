using SFARS.Application.Dtos.Auth;
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
        public DateTime CreateDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Security - internal use only (not serialized to response)
        [JsonIgnore]
        public string? PasswordHash { get; set; }
        
        [JsonIgnore]
        public bool TwoFactorEnabled { get; set; }

        // Role info
        public RoleDto? Role { get; set; }
        
        // Roles (flattened for API response)
        public IEnumerable<string>? Roles { get; set; }
    }

    public class RoleDto
    {
        public Guid Id { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public static class UserDtoExtensions
    {
        public static AuthenticateUserDto ToAuthenticateUserDto(this UserDto userDto)
        {
            return new AuthenticateUserDto
            {
                Email = userDto.Email,
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Phone = userDto.Phone,
                Avatar = userDto.Avatar,
                Address = userDto.Address,
                Gender = userDto.Gender?.ToString(),
                Dob = userDto.Dob,
                IsActive = userDto.Status == UserStatus.Active,
                CreateDate = userDto.CreateDate,
                ModifiedDate = userDto.ModifiedDate
            };
        }
    }
}
