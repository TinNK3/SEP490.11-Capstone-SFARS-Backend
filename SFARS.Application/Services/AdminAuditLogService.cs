using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Admin;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.AdminAuditLogs;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services
{
    /// <summary>
    /// Service for recording and querying Admin audit logs.
    /// </summary>
    public class AdminAuditLogService : IAdminAuditLogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemMessageService _msgService;
        private readonly IMapper _mapper;
        private readonly ILogger<AdminAuditLogService> _logger;

        public AdminAuditLogService(
            IUnitOfWork unitOfWork,
            ISystemMessageService msgService,
            IMapper mapper,
            ILogger<AdminAuditLogService> logger)
        {
            _unitOfWork = unitOfWork;
            _msgService = msgService;
            _mapper = mapper;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task LogAsync(
            Guid adminId,
            AdminAction action,
            string entityType,
            Guid entityId,
            string? oldValue,
            string? newValue,
            string? reason = null,
            string? ipAddress = null)
        {
            var log = new AdminAuditLog
            {
                AdminId    = adminId,
                Action     = action,
                EntityType = entityType,
                EntityId   = entityId,
                OldValue   = oldValue,
                NewValue   = newValue,
                Reason     = reason,
                IpAddress  = ipAddress,
                CreatedAt  = DateTime.UtcNow,
                CreatedBy  = adminId
            };

            await _unitOfWork.Repository<AdminAuditLog, Guid>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[AuditLog] Admin {AdminId} performed {Action} on {EntityType} {EntityId}",
                adminId, action, entityType, entityId);
        }

        /// <inheritdoc />
        public async Task<IServiceResult> GetLogsAsync(
            AdminAuditLogSpecParams specParams)
        {
            try
            {
                var listSpec  = AdminAuditLogSpecification.List(specParams);
                var countSpec = AdminAuditLogSpecification.Count(specParams);

                var logs       = await _unitOfWork.Repository<AdminAuditLog, Guid>().GetAllWithSpecAsync(listSpec);
                var totalItems = await _unitOfWork.Repository<AdminAuditLog, Guid>().CountAsync(countSpec);
                var limit = specParams.GetTake();
                var totalPages = (int)Math.Ceiling((double)totalItems / limit);

                var dtos = _mapper.Map<List<AdminAuditLogDto>>(logs);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                    new PaginatedResultDto<AdminAuditLogDto>(dtos, specParams.GetPage(), limit, totalPages, totalItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin audit logs");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IServiceResult> GetLogsByEntityAsync(
            string entityType, Guid entityId, BaseSpecParams specParams)
        {
            try
            {
                var spec = AdminAuditLogSpecification.ByEntityId(entityType, entityId, specParams);
                var logs = await _unitOfWork.Repository<AdminAuditLog, Guid>().GetAllWithSpecAsync(spec);

                // Count for the same entity
                var countParams = new AdminAuditLogSpecParams
                {
                    EntityType = entityType,
                    EntityId   = entityId
                };
                var countSpec  = AdminAuditLogSpecification.Count(countParams);
                var totalItems = await _unitOfWork.Repository<AdminAuditLog, Guid>().CountAsync(countSpec);
                var limit = specParams.GetTake();
                var totalPages = (int)Math.Ceiling((double)totalItems / limit);

                var dtos = _mapper.Map<List<AdminAuditLogDto>>(logs);

                return new ServiceResult(
                    ResultCodeConst.SYS_Success0002,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                    new PaginatedResultDto<AdminAuditLogDto>(dtos, specParams.GetPage(), limit, totalPages, totalItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

    }
}
