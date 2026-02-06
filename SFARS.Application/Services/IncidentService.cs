using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Validations;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services
{
    public class IncidentService : GenericService<Incident, IncidentDto, Guid>, IIncidentService<IncidentDto>
    {

        public IncidentService(
            ISystemMessageService msgService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<IncidentService> logger)
            : base(msgService, unitOfWork, mapper, logger)
        {
        }

        /// <summary>
        /// Create a new incident (SOS report) with all related entities in single atomic transaction
        /// </summary>
        public async Task<IServiceResult> CreateIncidentAsync(Guid userId, IncidentDto dto)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var user = await _unitOfWork.Repository<User, Guid>().GetByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "User")
                );
            }

            var validation = await ValidatorExtensions.ValidateAsync(dto);
            if (validation != null && !validation.IsValid)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001),
                    validation.ToProblemDetails().Errors
                );
            }

            // Cache system messages
            var reasonMsg = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Reason0001);
            var notifyTitle = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0001);
            var notifyTemplate = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Notify0002);
            var successMsg = await _msgService.GetMessageAsync(ResultCodeConst.Incident_Success0001);
            var failMsg = await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001);

            // Generate incident code using SQL SEQUENCE
            var now = DateTime.UtcNow;
            var sequenceValue = await _unitOfWork.GetNextSequenceValueAsync(SequenceNames.IncidentCode);
            var incidentCode = $"SOS-{now.Year}-{sequenceValue:D5}";

            // Create Point from lat/lng
            var location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };

            // Create incident entity
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                Code = incidentCode,
                VictimId = userId,
                Location = location,
                AddressString = dto.AddressString,
                Description = dto.Description,
                CurrentStatus = IncidentStatus.Pending,
                PriorityLevel = dto.PriorityLevel,
                CreatedAt = now,
                CreatedBy = userId
            };

            // Create IncidentStatusHistory (audit log)
            var statusHistory = new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                StatusFrom = null,
                StatusTo = IncidentStatus.Pending,
                ChangedBy = userId,
                ChangeReason = reasonMsg,
                CreatedAt = now,
                CreatedBy = userId
            };

            // Create IncidentChat (empty shell for future AI chat)
            var chat = new IncidentChat
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                Title = $"Chat - {incidentCode}",
                CreatedAt = now,
                CreatedBy = userId
            };

            // Create NotificationLog for victim (confirmation)
            var notification = new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = notifyTitle,
                Message = string.Format(notifyTemplate, incidentCode),
                Type = NotificationType.Mission,
                IsRead = false,
                SentAt = now,
                CreatedAt = now,
                CreatedBy = userId
            };

            // Add all entities to repositories
            await _unitOfWork.Repository<Incident, Guid>().AddAsync(incident);
            await _unitOfWork.Repository<IncidentStatusHistory, Guid>().AddAsync(statusHistory);
            await _unitOfWork.Repository<IncidentChat, Guid>().AddAsync(chat);
            await _unitOfWork.Repository<NotificationLog, Guid>().AddAsync(notification);

            // Save - atomic transaction
            var saveResult = await _unitOfWork.SaveChangesAsync();

            if (saveResult > 0)
            {
                var resultDto = _mapper.Map<IncidentDto>(incident);
                resultDto.VictimName = user.FullName;
                resultDto.Latitude = dto.Latitude;
                resultDto.Longitude = dto.Longitude;

                return new ServiceResult(
                    ResultCodeConst.Incident_Success0001,
                    successMsg,
                    resultDto
                );
            }

            return new ServiceResult(ResultCodeConst.SYS_Fail0001, failMsg);
        }

        /// <summary>
        /// Get incidents reported by the current user
        /// </summary>
        public async Task<IServiceResult> GetMyIncidentsAsync(Guid userId, int pageIndex = 0, int pageSize = 10)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var spec = new BaseSpecification<Incident>(i => i.VictimId == userId);
            spec.AddOrderByDescending(i => i.CreatedAt);
            spec.ApplyPaging(pageSize, pageIndex * pageSize);

            var dtos = await _unitOfWork.Repository<Incident, Guid>()
                .GetAllWithSpecAndSelectorAsync(spec, i => new IncidentDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    Latitude = i.Location.Y,
                    Longitude = i.Location.X,
                    AddressString = i.AddressString,
                    Description = i.Description,
                    CurrentStatus = i.CurrentStatus,
                    PriorityLevel = i.PriorityLevel,
                    AiPredictionResult = i.AiPredictionResult,
                    AiConfidenceScore = i.AiConfidenceScore,
                    SnakeId = i.SnakeId,
                    VictimId = i.VictimId,
                    VictimName = i.Victim.FullName,
                    CreatedAt = i.CreatedAt
                }, tracked: false);

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dtos
            );
        }

        /// <summary>
        /// Get incident by ID with authorization check (user must be victim or rescuer)
        /// </summary>
        public async Task<IServiceResult> GetIncidentByIdAsync(Guid userId, Guid incidentId)
        {
            if (userId == Guid.Empty)
            {
                return new ServiceResult(
                    ResultCodeConst.Auth_Warning0013,
                    await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0013)
                );
            }

            var spec = new BaseSpecification<Incident>(i =>
                i.Id == incidentId &&
                (
                    i.VictimId == userId ||
                    i.Missions.Any(m => m.RescuerId == userId)
                )
            );

            var dto = await _unitOfWork.Repository<Incident, Guid>()
                .GetWithSpecAndSelectorAsync(spec, i => new IncidentDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    Latitude = i.Location.Y,
                    Longitude = i.Location.X,
                    AddressString = i.AddressString,
                    Description = i.Description,
                    CurrentStatus = i.CurrentStatus,
                    PriorityLevel = i.PriorityLevel,
                    AiPredictionResult = i.AiPredictionResult,
                    AiConfidenceScore = i.AiConfidenceScore,
                    SnakeId = i.SnakeId,
                    VictimId = i.VictimId,
                    VictimName = i.Victim.FullName,
                    CreatedAt = i.CreatedAt
                }, tracked: false);

            if (dto == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0004,
                    await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004)
                );
            }

            return new ServiceResult(
                ResultCodeConst.SYS_Success0002,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
                dto
            );
        }
    }
}