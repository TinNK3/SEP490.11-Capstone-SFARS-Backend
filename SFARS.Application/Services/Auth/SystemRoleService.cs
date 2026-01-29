using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Role;
using SFARS.Application.Services;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services.Auth
{
    public class SystemRoleService : GenericService<Role, SystemRoleDto, Guid>, ISystemRoleService<SystemRoleDto>
    {
        public SystemRoleService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<SystemRoleService> logger) : base(msgService, unitOfWork, mapper, logger)
        {
        }

        /// <summary>
        /// Get role by name (e.g., "User", "Admin", "Rescuer")
        /// </summary>
        /// <param name="roleName">Name of the role</param>
        /// <returns>ServiceResult with role data or not found</returns>
        public async Task<IServiceResult> GetRoleByNameAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0002, 
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002));
            }

            var spec = new RoleSpecification(roleName);
            var role = await _unitOfWork.Repository<Role, Guid>().GetWithSpecAsync(spec);

            if (role == null)
            {
                return new ServiceResult(ResultCodeConst.SYS_Warning0004, 
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
            }

            var roleDto = _mapper.Map<SystemRoleDto>(role);
            return new ServiceResult(ResultCodeConst.SYS_Success0002, string.Empty, roleDto);
        }
    }
}