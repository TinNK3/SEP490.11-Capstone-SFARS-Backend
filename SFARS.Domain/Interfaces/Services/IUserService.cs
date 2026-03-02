using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Interfaces.Services
{
    public interface IUserService<TDto> : IGenericService<User, TDto, Guid>
        where TDto : class
    {
        Task<IServiceResult> GetByEmailAsync(string email);
        Task<IServiceResult> GetMeAsync(Guid userId);
        Task<IServiceResult> UpdateMeAsync(Guid userId, TDto dto);
        Task<IServiceResult> UpdateEmailVerificationCodeAsync(Guid userId, string otp);
        Task<IServiceResult> UpdatePasswordAsync(Guid userId, string newPasswordHash);

        // Admin — paginated user list with filters
        Task<IServiceResult> GetAllUsersAsync(UserSpecParams specParams, int pageIndex, int pageSize);

        // Admin — get single user by ID
        Task<IServiceResult> GetUserByIdAsync(Guid userId);

        // Admin — update user status (Active / Inactive / Banned / Deleted)
        Task<IServiceResult> UpdateUserStatusAsync(Guid adminId, Guid targetUserId, UserStatus newStatus, string? reason);

        // Admin — create a new user account and assign a role
        Task<IServiceResult> CreateUserAsync(Guid adminId, TDto dto);

        // Location
        Task<IServiceResult> GetUserLocationAsync(Guid userId);
        Task<IServiceResult> UpdateUserLocationAsync(
            Guid userId, double latitude, double longitude, double? accuracyMeters);
    }
}