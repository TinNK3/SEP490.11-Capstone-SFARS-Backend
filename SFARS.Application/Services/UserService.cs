using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services
{
    public class UserService : GenericService<User, UserDto, Guid>, IUserService<UserDto>
    {
        public UserService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UserService> logger) : base(msgService, unitOfWork, mapper, logger)
        {
        }

        /// <summary>
        /// Get user by email address (for authentication)
        /// </summary>
        public async Task<IServiceResult> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0002, "Email is required");
            }

            var spec = UserSpecification.ByEmail(email);
            var user = await _unitOfWork.Repository<User, Guid>().GetWithSpecAsync(spec);

            if (user == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, "User not found");
            }

            // Map user to DTO with role info
            var userDto = _mapper.Map<UserDto>(user);
            
            // Map the first role (primary role)
            if (user.UserRoles.Any())
            {
                var primaryRole = user.UserRoles.First().Role;
                userDto.Role = new RoleDto
                {
                    Id = primaryRole.Id,
                    RoleName = primaryRole.RoleName,
                    Description = primaryRole.Description
                };
                userDto.Roles = user.UserRoles.Select(ur => ur.Role.RoleName);
            }

            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, userDto);
        }
    }
}
