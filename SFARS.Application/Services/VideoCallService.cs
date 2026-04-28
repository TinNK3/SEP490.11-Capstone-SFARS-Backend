using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Application.Common;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Models.VideoCall;
using SFARS.Domain.Specifications;
using Microsoft.AspNetCore.SignalR;
using SFARS.Infrastructure.Hubs;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Application.Services;

public class VideoCallService : IVideoCallService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgoraService _agoraService;
    private readonly IFcmPushService _fcmPushService;
    private readonly ISystemMessageService _msgService;
    private readonly IHubContext<LocationTrackingHub> _hubContext;
    private readonly ILogger<VideoCallService> _logger;
    private readonly AgoraOptions _agoraOptions;

    public VideoCallService(
        IUnitOfWork unitOfWork,
        IAgoraService agoraService,
        IFcmPushService fcmPushService,
        ISystemMessageService msgService,
        IHubContext<LocationTrackingHub> hubContext,
        ILogger<VideoCallService> logger,
        IOptions<AgoraOptions> agoraOptions)
    {
        _unitOfWork = unitOfWork;
        _agoraService = agoraService;
        _fcmPushService = fcmPushService;
        _msgService = msgService;
        _hubContext = hubContext;
        _logger = logger;
        _agoraOptions = agoraOptions.Value;
    }

    public async Task<IServiceResult> GetJoinTokenAsync(Guid incidentId, Guid userId)
    {
        try
        {
            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
            if (incident == null)
            {
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "Incident"));
            }

            // Authorization: Must be victim or assigned rescuer
            bool isVictim = incident.VictimId == userId;
            bool isRescuer = false;

            // Find active mission for this incident
            var missionSpec = new BaseSpecification<RescueMission>(m =>
                m.IncidentId == incidentId
                && m.Status != RescueStatus.Completed
                && m.Status != RescueStatus.Rejected
                && m.Status != RescueStatus.Reassigned);

            var mission = (await _unitOfWork.Repository<RescueMission, Guid>()
                .GetAllWithSpecAsync(missionSpec, tracked: false))
                .FirstOrDefault();

            if (mission != null && mission.RescuerId == userId)
            {
                isRescuer = true;
            }

            if (!isVictim && !isRescuer)
            {
                return new ServiceResult(
                    ResultCodeConst.VideoCall_Warning0001,
                    await _msgService.GetMessageAsync(ResultCodeConst.VideoCall_Warning0001));
            }

            // Project Guid hash to uint32 for Agora compliance
            uint uid = (uint)(userId.GetHashCode() & 0x7FFFFFFF);

            string channelName = incidentId.ToString();
            string token = _agoraService.GenerateRtcToken(channelName, uid, _agoraOptions.TokenExpirationSeconds);

            if (string.IsNullOrEmpty(token))
            {
                return new ServiceResult(
                    ResultCodeConst.VideoCall_Warning0003,
                    await _msgService.GetMessageAsync(ResultCodeConst.VideoCall_Warning0003));
            }

            var result = new VideoCallTokenResponseDto
            {
                AppId = _agoraOptions.AppId,
                ChannelName = channelName,
                Token = token,
                Uid = uid
            };

            return new ServiceResult(
                ResultCodeConst.VideoCall_Success0001,
                await _msgService.GetMessageAsync(ResultCodeConst.VideoCall_Success0001), result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating video call token for Incident {IncidentId}", incidentId);
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }

    public async Task<IServiceResult> InitiateCallAsync(Guid incidentId, Guid callerId)
    {
        try
        {
            _logger.LogInformation(
                "Video call initiation requested | IncidentId={IncidentId} | CallerId={CallerId}",
                incidentId, callerId);

            var incident = await _unitOfWork.Repository<Incident, Guid>().GetByIdAsync(incidentId);
            if (incident == null)
            {
                _logger.LogWarning(
                    "Video call initiation denied | IncidentId={IncidentId} | CallerId={CallerId} | Reason=IncidentNotFound",
                    incidentId, callerId);
                return new ServiceResult(
                    ResultCodeConst.SYS_Warning0002,
                    string.Format(await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0002), "Incident"));
            }

            // Determine target user and caller role name
            Guid targetUserId;
            string callerRoleName;

            if (incident.VictimId == callerId)
            {
                // Victim calling Rescuer — find active mission
                var missionSpec = new BaseSpecification<RescueMission>(m =>
                    m.IncidentId == incidentId
                    && m.Status != RescueStatus.Completed
                    && m.Status != RescueStatus.Rejected
                    && m.Status != RescueStatus.Reassigned);

                var mission = (await _unitOfWork.Repository<RescueMission, Guid>()
                    .GetAllWithSpecAsync(missionSpec, tracked: false))
                    .FirstOrDefault();

                if (mission == null)
                {
                    _logger.LogWarning(
                        "Video call initiation denied | IncidentId={IncidentId} | CallerId={CallerId} | Reason=ActiveMissionNotFound",
                        incidentId, callerId);
                    return new ServiceResult(
                        ResultCodeConst.VideoCall_Warning0002,
                        await _msgService.GetMessageAsync(ResultCodeConst.VideoCall_Warning0002));
                }

                targetUserId = mission.RescuerId;
                callerRoleName = VideoCallConstants.LabelCallerVictim;
            }
            else
            {
                // Rescuer calling Victim
                targetUserId = incident.VictimId;
                callerRoleName = VideoCallConstants.LabelCallerRescuer;
            }

            var pushTitle = VideoCallConstants.PushTitle;
            var pushBody = string.Format(VideoCallConstants.PushBodyTemplate, callerRoleName);

            // Send FCM High-Priority Push to wake up the app
            var pushData = new Dictionary<string, string>
            {
                { VideoCallConstants.FcmCallTypeKey, VideoCallConstants.FcmCallTypeValue },
                { VideoCallConstants.FcmIncidentIdKey, incidentId.ToString() },
                { VideoCallConstants.FcmCallerNameKey, callerRoleName }
            };

            await _fcmPushService.SendToUserAsync(targetUserId, pushTitle, pushBody, pushData);

            _logger.LogInformation(
                "Video call FCM sent | IncidentId={IncidentId} | CallerId={CallerId} | TargetUserId={TargetUserId} | CallerRole={CallerRole}",
                incidentId, callerId, targetUserId, callerRoleName);

            // Send SignalR incoming-call event on the tracking hub so the client can
            // handle the entire rescue-call lifecycle from a single hub contract.
            var payload = new
            {
                IncidentId = incidentId,
                CallerId = callerId,
                CallerName = callerRoleName
            };

            _logger.LogInformation(
                "Sending SignalR incoming video call | Hub={Hub} | Event={Event} | IncidentId={IncidentId} | CallerId={CallerId} | TargetUserId={TargetUserId} | Payload={Payload}",
                nameof(LocationTrackingHub), VideoCallConstants.EventCallIncoming, incidentId, callerId, targetUserId, payload);

            await _hubContext.Clients.User(targetUserId.ToString()).SendAsync(VideoCallConstants.EventCallIncoming, new
            {
                IncidentId = incidentId,
                CallerId = callerId,
                CallerName = callerRoleName
            });

            _logger.LogInformation(
                "SignalR incoming video call sent | IncidentId={IncidentId} | CallerId={CallerId} | TargetUserId={TargetUserId} | Event={Event}",
                incidentId, callerId, targetUserId, VideoCallConstants.EventCallIncoming);

            return new ServiceResult(
                ResultCodeConst.SYS_Success0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating video call for Incident {IncidentId}", incidentId);
            return new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001));
        }
    }
}
