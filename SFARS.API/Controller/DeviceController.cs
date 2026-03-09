using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Device;
using SFARS.Application.Services;
using SFARS.Domain.Common;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Device management endpoints (FCM Token)
    /// </summary>
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DeviceController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        /// <summary>
        /// Register or update Firebase Cloud Messaging token for push notifications
        /// </summary>
        /// <param name="dto">Device token</param>
        /// <returns>Registration result</returns>
        [Authorize]
        [HttpPost(APIRoute.Device.RegisterToken, Name = nameof(RegisterDeviceTokenAsync))]
        public async Task<IActionResult> RegisterDeviceTokenAsync([FromBody] RegisterDeviceTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token) || dto.Token.Length < 50)
            {
                return this.ToIActionResult(new ServiceResult(ResultCodeConst.SYS_Warning0001, "Token must be at least 50 characters"));
            }

            var userId = User.GetUserId();
            await _deviceService.RegisterDeviceTokenAsync(userId, dto.Token, dto.Platform);
            
            return this.ToIActionResult(new ServiceResult(ResultCodeConst.SYS_Success0003, "Device token registered", true));
        }

        /// <summary>
        /// Unregister Firebase Cloud Messaging token when user logs out
        /// </summary>
        /// <param name="token">Device token</param>
        /// <returns>Unregistration result</returns>
        [Authorize]
        [HttpDelete(APIRoute.Device.UnregisterToken, Name = nameof(UnregisterDeviceTokenAsync))]
        public async Task<IActionResult> UnregisterDeviceTokenAsync([FromRoute] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return this.ToIActionResult(new ServiceResult(ResultCodeConst.SYS_Warning0001, "Token is required"));
            }

            var userId = User.GetUserId();
            await _deviceService.UnregisterDeviceTokenAsync(userId, token);
            
            return this.ToIActionResult(new ServiceResult(ResultCodeConst.SYS_Success0003, "Device token unregistered", true));
        }
    }
}