using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Admin;
using SFARS.API.Payloads.Request.User;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// User profile endpoints
    /// </summary>
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService<UserDto> _userService;
        private readonly ITransactionService<TransactionDto> _transactionService;

        public UserController(IUserService<UserDto> userService, ITransactionService<TransactionDto> transactionService)
        {
            _userService = userService;
            _transactionService = transactionService;
        }

        #region Me Profile

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

        #endregion

        #region Admin Manage Users

        /// <summary>
        /// [Admin] Get paginated user list with optional filters.
        /// </summary>
        /// <remarks>
        /// **Filters (all optional, combinable):**
        /// - `search`  — full-text search on name / email / phone
        /// - `role`    — role name: `User` | `Rescuer` | `Admin`
        /// - `status`  — enum: `active` | `inactive` | `banned` | `deleted`
        /// - `createDateRange[0]` / `createDateRange[1]` — registration date range (ISO 8601)
        ///
        /// **Sorting** (`sort` query param):
        /// - `FirstName` / `-FirstName` (prefix `-` = descending)
        /// - `Email`, `CreatedAt`, `Status`, etc.
        ///
        /// **Pagination:**
        /// - `page` (1-based, default: 1)
        /// - `limit` (default: 10)
        /// </remarks>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpGet(APIRoute.Admin.GetAllUsers, Name = nameof(GetAllUsersAsync))]
        public async Task<IActionResult> GetAllUsersAsync([FromQuery] UserSpecParams specParams)
        {
            var result = await _userService.GetAllUsersAsync(specParams, specParams.PageIndex, specParams.PageSize);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get a single user by ID.
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpGet(APIRoute.Admin.GetUserById, Name = nameof(GetUserByIdAsync))]
        public async Task<IActionResult> GetUserByIdAsync(Guid id)
        {
            var result = await _userService.GetUserByIdAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Update a user's status (`active` | `inactive` | `banned` | `deleted`).
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpPut(APIRoute.Admin.UpdateUserStatus, Name = nameof(UpdateUserStatusAsync))]
        public async Task<IActionResult> UpdateUserStatusAsync(Guid id, [FromBody] UpdateUserStatusRequest req)
        {
            var adminId = User.GetUserId();
            var result = await _userService.UpdateUserStatusAsync(adminId, id, req.Status, req.Reason);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Create a new user account with a specific role.
        /// Password is hashed server-side. Status defaults to Active.
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpPost(APIRoute.Admin.CreateUser, Name = nameof(CreateUserAsync))]
        public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest req)
        {
            var adminId = User.GetUserId();
            var result = await _userService.CreateUserAsync(adminId, req.ToUserDto());
            return this.ToIActionResult(result);
        }

        #endregion

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

        #region Donation History

        /// <summary>
        /// Get current user's donation history
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.User.MeDonationHistory, Name = nameof(GetMyDonationHistoryAsync))]
        public async Task<IActionResult> GetMyDonationHistoryAsync(
            [FromQuery] TransactionSpecParams specParams,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            var result = await _transactionService.GetMyTransactionsAsync(userId, specParams, pageIndex, pageSize);
            return this.ToIActionResult(result);
        }

        #endregion
    }
}