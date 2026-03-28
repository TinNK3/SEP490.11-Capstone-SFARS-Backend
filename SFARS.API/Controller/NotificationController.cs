using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Notification endpoints
    /// </summary>
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get paginated notifications for the authenticated user
        /// </summary>
        [HttpGet(APIRoute.Notifications.GetList, Name = nameof(GetNotificationsAsync))]
        public async Task<IActionResult> GetNotificationsAsync([FromQuery] NotificationSpecParams specParams)
        {
            var userId = User.GetUserId();
            var result = await _notificationService.GetNotificationsAsync(userId, specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet(APIRoute.Notifications.GetUnreadCount, Name = nameof(GetUnreadCountAsync))]
        public async Task<IActionResult> GetUnreadCountAsync()
        {
            var userId = User.GetUserId();
            var result = await _notificationService.GetUnreadCountAsync(userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Mark a specific notification as read
        /// </summary>
        [HttpPatch(APIRoute.Notifications.MarkAsRead, Name = nameof(MarkAsReadAsync))]
        public async Task<IActionResult> MarkAsReadAsync([FromRoute] Guid id)
        {
            var userId = User.GetUserId();
            var result = await _notificationService.MarkAsReadAsync(id, userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Mark all unread notifications as read
        /// </summary>
        [HttpPatch(APIRoute.Notifications.MarkAllAsRead, Name = nameof(MarkAllAsReadAsync))]
        public async Task<IActionResult> MarkAllAsReadAsync()
        {
            var userId = User.GetUserId();
            var result = await _notificationService.MarkAllAsReadAsync(userId);
            return this.ToIActionResult(result);
        }
    }
}
