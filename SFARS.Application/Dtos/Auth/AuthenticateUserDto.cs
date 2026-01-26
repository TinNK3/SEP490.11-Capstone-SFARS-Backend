using SFARS.Application.Dtos.User;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Application.Dtos.Auth
{
    /// <summary>
    /// DTO for authenticated user response (includes auth-specific fields)
    /// </summary>
    public class AuthenticateUserDto : BaseUser
    {
        public Guid Id { get; set; }
        
        // Authentication tokens
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserCode { get; set; }
        public string? Password { get; set; }
        public bool IsRescuer { get; set; }
        public string RoleName { get; set; } = null!;
    }

    public static class AuthenticateUserDtoExtensions
    {
        public static UserDto ToUserDto(this AuthenticateUserDto authenticateUser)
        {
            // Parse gender string to enum
            Gender? gender = null;
            if (!string.IsNullOrEmpty(authenticateUser.Gender) && 
                Enum.TryParse<Gender>(authenticateUser.Gender, out var parsedGender))
            {
                gender = parsedGender;
            }

            return new UserDto
            {
                Id = authenticateUser.Id,
                Email = authenticateUser.Email,
                FirstName = authenticateUser.FirstName,
                LastName = authenticateUser.LastName,
                Phone = authenticateUser.Phone,
                Avatar = authenticateUser.Avatar,
                Address = authenticateUser.Address,
                Gender = gender,
                Dob = authenticateUser.Dob,
                Status = authenticateUser.IsActive ? UserStatus.Active : UserStatus.Inactive,
                CreateDate = authenticateUser.CreateDate,
                ModifiedDate = authenticateUser.ModifiedDate
            };
        }
    }
}
