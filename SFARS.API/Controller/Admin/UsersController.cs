using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Admin;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller.Admin
{
    /// <summary>
    /// Admin — User Management (api/admin/users)
    /// </summary>
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService<UserDto> _service;

        public UsersController(IUserService<UserDto> service)
        {
            _service = service;
        }

        /// <summary>
        /// [Admin] Get paginated user list with optional filters.
        /// </summary>
        /// <remarks>
        /// **Filters (all optional, combinable):**
        /// - `search`  — full-text search on name / email / phone
        /// - `role`    — role name: `User` | `Rescuer` | `Admin`
        /// - `status`  — enum: `Active` | `Inactive` | `Banned` | `Deleted`
        /// - `createDateRange[0]` / `createDateRange[1]` — registration date range (ISO 8601)
        ///
        /// **Sorting** (`sort` query param):
        /// - `FirstName` / `-FirstName` (prefix `-` = descending)
        /// - `Email`, `CreatedAt`, `Status`, etc.
        ///
        /// **Pagination:**
        /// - `pageIndex` (0-based, default: 0)
        /// - `pageSize` (default: 10)
        /// </remarks>
        [HttpGet(APIRoute.Admin.GetAllUsers, Name = nameof(GetAllUsersAsync))]
        public async Task<IActionResult> GetAllUsersAsync(
            [FromQuery] UserSpecParams specParams,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllUsersAsync(specParams, pageIndex, pageSize);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get a single user by ID.
        /// </summary>
        [HttpGet(APIRoute.Admin.GetUserById, Name = nameof(GetUserByIdAsync))]
        public async Task<IActionResult> GetUserByIdAsync(Guid id)
        {
            var result = await _service.GetUserByIdAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Update a user's status (Active | Inactive | Banned | Deleted).
        /// </summary>
        [HttpPut(APIRoute.Admin.UpdateUserStatus, Name = nameof(UpdateUserStatusAsync))]
        public async Task<IActionResult> UpdateUserStatusAsync(Guid id, [FromBody] UpdateUserStatusRequest req)
        {
            var adminId = User.GetUserId();
            var result  = await _service.UpdateUserStatusAsync(adminId, id, req.Status, req.Reason);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Create a new user account with a specific role.
        /// Password is hashed server-side. Status defaults to Active.
        /// </summary>
        [HttpPost(APIRoute.Admin.CreateUser, Name = nameof(CreateUserAsync))]
        public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest req)
        {
            var result = await _service.CreateUserAsync(req.ToUserDto());
            return this.ToIActionResult(result);
        }
    }
}
