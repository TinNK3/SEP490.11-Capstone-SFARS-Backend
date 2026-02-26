using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.User;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller
{
    /// <summary>
    /// User profile endpoints
    /// </summary>
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService<UserDto> _userService;

        public UserController(IUserService<UserDto> userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Get current authenticated user's profile
        /// </summary>
        /// <returns>User profile information</returns>
        [Authorize]
        [HttpGet(APIRoute.User.Me, Name = nameof(GetMeAsync))]
        public async Task<IActionResult> GetMeAsync()
        {
            var userId = User.GetUserId();

            var result = await _userService.GetMeAsync(userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Update current authenticated user's profile
        /// </summary>
        /// <param name="req">Profile update request</param>
        /// <returns>Updated user profile</returns>
        [Authorize]
        [HttpPut(APIRoute.User.UpdateMe, Name = nameof(UpdateMeAsync))]
        public async Task<IActionResult> UpdateMeAsync([FromBody] UpdateProfileRequest req)
        {
            var userId = User.GetUserId();

            var result = await _userService.UpdateMeAsync(userId, req.ToUserForUpdate());
            return this.ToIActionResult(result);
        }

        #region Location

        /// <summary>
        /// Get current user's real-time location
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.User.MeLocation, Name = nameof(GetMyLocation))]
        public async Task<IActionResult> GetMyLocation()
        {
            var userId = User.GetUserId();
            var result = await _userService.GetUserLocationAsync(userId);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// Update current user's real-time location (called from mobile GPS)
        /// </summary>
        [Authorize]
        [HttpPut(APIRoute.User.MeLocation, Name = nameof(UpdateMyLocation))]
        public async Task<IActionResult> UpdateMyLocation([FromBody] UpdateLocationRequest req)
        {
            var userId = User.GetUserId();
            var result = await _userService.UpdateUserLocationAsync(
                userId, req.Latitude, req.Longitude, req.AccuracyMeters);
            return this.ToIActionResult(result);
        }

        #endregion
    }
}