using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using System.Text.Json;
using SFARS.Domain.Specifications;
using NotificationTypeEnum = SFARS.Domain.Common.Enum.NotificationType;

namespace SFARS.Infrastructure.Services;

public class FcmPushService : IFcmPushService
{
    private readonly ILogger<FcmPushService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly bool _isInitialized;

    public FcmPushService(IConfiguration config, ILogger<FcmPushService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;

        var credentialsPath = config["Firebase:CredentialsPath"];
        if (string.IsNullOrEmpty(credentialsPath) || !File.Exists(credentialsPath))
        {
            _logger.LogWarning("Firebase:CredentialsPath is missing or file does not exist. FCM push is disabled.");
            _isInitialized = false;
            return;
        }

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(credentialsPath),
                    ProjectId = config["Firebase:ProjectId"]
                });
            }
            _isInitialized = true;
        }
        catch (ArgumentException)
        {
            // Already initialized
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FirebaseApp");
            _isInitialized = false;
        }
    }

    public async Task SendToUserAsync(Guid userId, string title, string body, IDictionary<string, string>? data = null)
    {
        await SendToUsersAsync(new[] { userId }, title, body, data);
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body, IDictionary<string, string>? data = null)
    {
        if (!_isInitialized) return;

        var userIdsList = userIds.Distinct().ToList();
        if (userIdsList.Count == 0) return;

        // Note: Using Tracked=true to allow deletion of stale tokens if needed
        var spec = new BaseSpecification<UserDevice>(x => userIdsList.Contains(x.UserId));
        var userDevices = await _unitOfWork.Repository<UserDevice, Guid>()
            .GetAllWithSpecAsync(spec, tracked: true);

        var validDevices = userDevices.Where(d => !string.IsNullOrEmpty(d.DeviceToken)).ToList();
        if (validDevices.Count == 0) return;

        var tokens = validDevices.Select(d => d.DeviceToken).Distinct().ToList();

        // Prepare message payload
        var messageData = data ?? new Dictionary<string, string>();
        
        try
        {
            // Firebase limits multicast to 500 tokens. Chunking protects against limits.
            var chunks = tokens.Chunk(500);
            var failedTokens = new List<UserDevice>();
            var successfulUserIds = new HashSet<Guid>();

            foreach (var chunk in chunks)
            {
                var multicastMessage = new MulticastMessage()
                {
                    Tokens = chunk.ToList(),
                    Notification = new Notification()
                    {
                        Title = title,
                        Body = body
                    },
                    Data = new Dictionary<string, string>(messageData)
                };

                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);
                _logger.LogInformation("FCM SendEachForMulticastAsync: Success: {Success}, Failure: {Fail}", response.SuccessCount, response.FailureCount);

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var tokenStr = chunk[i];
                    var device = validDevices.FirstOrDefault(d => d.DeviceToken == tokenStr);
                    
                    if (response.Responses[i].IsSuccess)
                    {
                        if (device != null) successfulUserIds.Add(device.UserId);
                    }
                    else
                    {
                        var exception = response.Responses[i].Exception;
                        // MessagingErrorCode -> Unregistered means token is no longer valid
                        var errCode = exception?.MessagingErrorCode;
                        if (errCode == MessagingErrorCode.Unregistered || 
                            errCode == MessagingErrorCode.InvalidArgument ||
                            errCode == MessagingErrorCode.SenderIdMismatch)
                        {
                            if (device != null) failedTokens.Add(device);
                        }
                    }
                }
            }

            if (failedTokens.Count > 0)
            {
                var deadIds = failedTokens.Select(d => d.Id).ToArray();
                await _unitOfWork.Repository<UserDevice, Guid>().DeleteRangeAsync(deadIds);
                _logger.LogInformation("Cleaned up {Count} stale UserDevices.", failedTokens.Count);
            }

            // Save NotificationLog using Bulk Insert
            if (successfulUserIds.Count > 0)
            {
                var now = DateTime.UtcNow;
                NotificationTypeEnum type = NotificationTypeEnum.Alert;
                if (data != null && data.TryGetValue("type", out var typeStr) && typeStr == "sos_dispatch")
                {
                    type = NotificationTypeEnum.SosDispatch;
                }

                var logs = successfulUserIds.Select(gId => new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = gId,
                    Title = title,
                    Message = body,
                    Type = type,
                    IsRead = false,
                    SentAt = now,
                    CreatedAt = now,
                    CreatedBy = gId
                }).ToList();

                await _unitOfWork.Repository<NotificationLog, Guid>().AddRangeAsync(logs);
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FCM Multicast");
        }
    }

    //public async Task SendToUserWithoutLogAsync(Guid userId, string title, string body, IDictionary<string, string>? data = null)
    //{
    //    await SendToUsersWithoutLogAsync(new[] { userId }, title, body, data);
    //}

    //public async Task SendToUsersWithoutLogAsync(IEnumerable<Guid> userIds, string title, string body, IDictionary<string, string>? data = null)
    //{
    //    if (!_isInitialized) return;

    //    var userIdsList = userIds.Distinct().ToList();
    //    if (userIdsList.Count == 0) return;

    //    var spec = new BaseSpecification<UserDevice>(x => userIdsList.Contains(x.UserId));
    //    var userDevices = await _unitOfWork.Repository<UserDevice, Guid>()
    //        .GetAllWithSpecAsync(spec, tracked: true);

    //    var validDevices = userDevices.Where(d => !string.IsNullOrEmpty(d.DeviceToken)).ToList();
    //    if (validDevices.Count == 0) return;

    //    var tokens = validDevices.Select(d => d.DeviceToken).Distinct().ToList();
    //    var messageData = data ?? new Dictionary<string, string>();
        
    //    try
    //    {
    //        var chunks = tokens.Chunk(500);
    //        var failedTokens = new List<UserDevice>();

    //        foreach (var chunk in chunks)
    //        {
    //            var multicastMessage = new MulticastMessage()
    //            {
    //                Tokens = chunk.ToList(),
    //                Notification = new Notification()
    //                {
    //                    Title = title,
    //                    Body = body
    //                },
    //                Data = new Dictionary<string, string>(messageData)
    //            };

    //            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);
    //            _logger.LogInformation("FCM SendEachForMulticastAsync (WithoutLog): Success: {Success}, Failure: {Fail}", response.SuccessCount, response.FailureCount);

    //            for (var i = 0; i < response.Responses.Count; i++)
    //            {
    //                var tokenStr = chunk[i];
    //                var device = validDevices.FirstOrDefault(d => d.DeviceToken == tokenStr);
                    
    //                if (!response.Responses[i].IsSuccess)
    //                {
    //                    var exception = response.Responses[i].Exception;
    //                    var errCode = exception?.MessagingErrorCode;
    //                    if (errCode == MessagingErrorCode.Unregistered || 
    //                        errCode == MessagingErrorCode.InvalidArgument ||
    //                        errCode == MessagingErrorCode.SenderIdMismatch)
    //                    {
    //                        if (device != null) failedTokens.Add(device);
    //                    }
    //                }
    //            }
    //        }

    //        if (failedTokens.Count > 0)
    //        {
    //            var deadIds = failedTokens.Select(d => d.Id).ToArray();
    //            await _unitOfWork.Repository<UserDevice, Guid>().DeleteRangeAsync(deadIds);
    //            _logger.LogInformation("Cleaned up {Count} stale UserDevices.", failedTokens.Count);
    //            await _unitOfWork.SaveChangesAsync();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error sending FCM Multicast (WithoutLog)");
    //    }
    //}
}