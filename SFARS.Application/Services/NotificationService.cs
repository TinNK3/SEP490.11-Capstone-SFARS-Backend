using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Notification;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Application.Interfaces.Services;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Hubs;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Application.Services;

public class NotificationService : INotificationService
{
    private readonly ISystemMessageService _msgService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ISystemMessageService msgService,
        IUnitOfWork unitOfWork,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _msgService = msgService;
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<IServiceResult> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, Guid? referenceId = null)
    {
        return await SendNotificationsAsync(new[] { userId }, title, message, type, referenceId);
    }

    public async Task<IServiceResult> SendNotificationsAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type, Guid? referenceId = null)
    {
        var userIdsList = userIds.Distinct().ToList();
        if (!userIdsList.Any()) 
            return new ServiceResult(ResultCodeConst.SYS_Warning0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0001));

        var now = DateTime.UtcNow;
        var logs = userIdsList.Select(gId => new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = gId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            IsRead = false,
            SentAt = now,
            CreatedAt = now,
            CreatedBy = gId
        }).ToList();

        // 1. Save to DB
        await _unitOfWork.Repository<NotificationLog, Guid>().AddRangeAsync(logs);
        await _unitOfWork.SaveChangesAsync();

        // 2. Dispatch SignalR song song (Parallel) & Bỏ qua count query
        var signalRTasks = logs.Select(async log => 
        {
            var dto = new NotificationDto
            {
                Id = log.Id,
                Title = log.Title,
                Message = log.Message,
                Type = log.Type,
                ReferenceId = log.ReferenceId,
                IsRead = log.IsRead,
                SentAt = log.SentAt
            };
            
            // Gửi thông báo mới
            await _hubContext.Clients.User(log.UserId.ToString()).SendAsync("ReceiveNotification", dto);
            
            // CẮT BỎ CÂU QUERY N+1 TẠI ĐÂY! 
            // Thay vì gửi Count chính xác, chỉ gửi một trigger để Client tự biết đường +1 vào UI
            await _hubContext.Clients.User(log.UserId.ToString()).SendAsync("IncrementUnreadCount");
        });

        // Chạy toàn bộ tiến trình SignalR cùng lúc thay vì đợi từng cái
        await Task.WhenAll(signalRTasks);
        
        return new ServiceResult(ResultCodeConst.SYS_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002));
    }

    public async Task<IServiceResult> GetNotificationsAsync(Guid userId, NotificationSpecParams specParams)
    {
        var spec = new BaseSpecification<NotificationLog>(x => x.UserId == userId);
        if (specParams.IsRead.HasValue)
        {
            spec.AddFilter(x => x.IsRead == specParams.IsRead.Value);
        }
        spec.AddOrderByDescending(x => x.SentAt);
        
        var totalItems = await _unitOfWork.Repository<NotificationLog, Guid>().CountAsync(spec);
        
        if (totalItems == 0)
        {
            var emptyResult = new PaginatedResultDto<NotificationDto>(
                new List<NotificationDto>(), specParams.GetPage(), specParams.GetTake(), 0, 0);
            return new ServiceResult(ResultCodeConst.SYS_Success0002, 
                await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002), emptyResult);
        }

        spec.ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        var logs = await _unitOfWork.Repository<NotificationLog, Guid>().GetAllWithSpecAsync(spec);

        var dtos = logs.Select(x => new NotificationDto
        {
            Id = x.Id,
            Title = x.Title,
            Message = x.Message,
            Type = x.Type,
            ReferenceId = x.ReferenceId,
            IsRead = x.IsRead,
            SentAt = x.SentAt
        }).ToList();

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)specParams.GetTake());
        
        var paginatedResult = new PaginatedResultDto<NotificationDto>(dtos, specParams.GetPage(), specParams.GetTake(), totalPages, totalItems);
        return new ServiceResult(ResultCodeConst.SYS_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002), paginatedResult);
    }

    public async Task<IServiceResult> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var spec = new BaseSpecification<NotificationLog>(x => x.Id == notificationId && x.UserId == userId);
        var log = await _unitOfWork.Repository<NotificationLog, Guid>().GetWithSpecAsync(spec);
        
        if (log != null && !log.IsRead)
        {
            log.IsRead = true;
            _unitOfWork.Repository<NotificationLog, Guid>().Update(log);
            await _unitOfWork.SaveChangesAsync();
            
            var countSpec = new BaseSpecification<NotificationLog>(x => x.UserId == userId && !x.IsRead);
            var unreadCount = await _unitOfWork.Repository<NotificationLog, Guid>().CountAsync(countSpec);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", unreadCount);
        }
        
        return new ServiceResult(ResultCodeConst.SYS_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002));
    }

    public async Task<IServiceResult> MarkAllAsReadAsync(Guid userId)
    {
        var spec = new BaseSpecification<NotificationLog>(x => x.UserId == userId && !x.IsRead);
        
        // Execute Bulk Update (EF Core 7+) via GenericRepository pattern
        var affectedRows = await _unitOfWork.Repository<NotificationLog, Guid>().UpdateWithSpecAsync(
            spec,
            s => s.SetProperty(x => x.IsRead, true)
        );
        
        if (affectedRows > 0)
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", 0);
        }
        
        return new ServiceResult(ResultCodeConst.SYS_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002));
    }

    public async Task<IServiceResult> GetUnreadCountAsync(Guid userId)
    {
        var spec = new BaseSpecification<NotificationLog>(x => x.UserId == userId && !x.IsRead);
        var count = await _unitOfWork.Repository<NotificationLog, Guid>().CountAsync(spec);
        
        return new ServiceResult(ResultCodeConst.SYS_Success0002, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002), count);
    }
}
