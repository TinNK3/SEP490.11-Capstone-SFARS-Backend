using SFARS.Application.Dtos.User;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for authenticated user response (includes auth-specific fields)
    /// </summary>
    public class AuthUserDto 
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public string? Address { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? Dob { get; set; }
        public UserStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        
        // Authentication tokens
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Password { get; set; }
        public string? PasswordHash { get; set; }
        public string? EmailVerificationCode { get; set; }
        public bool IsRescuer { get; set; }
        public string RoleName { get; set; } = null!;
    }

    public static class AuthUserDtoExtensions
    {
        public static UserDto ToUserDto(
            this AuthUserDto authenticateUser)
        {
            return new UserDto
            {
                Id = authenticateUser.Id,
                Email = authenticateUser.Email,
                FirstName = authenticateUser.FirstName,
                LastName = authenticateUser.LastName,
                Phone = authenticateUser.Phone,
                PasswordHash = authenticateUser.PasswordHash,
                EmailVerificationCode = authenticateUser.EmailVerificationCode,
                Avatar = authenticateUser.Avatar,
                Address = authenticateUser.Address,
                Gender = authenticateUser.Gender,
                Dob = authenticateUser.Dob,
                Status = authenticateUser.Status,
                CreatedAt = authenticateUser.CreatedAt,
                UpdatedAt = authenticateUser.UpdatedAt,
                Role = authenticateUser.RoleName
            };
        }
    }
}