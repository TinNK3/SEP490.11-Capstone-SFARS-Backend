using Microsoft.AspNetCore.Mvc;
using SFARS.Application.Common;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.API.Extensions
{
    public static class ControllerExtensions
    {
        public static IActionResult ToIActionResult(this ControllerBase controller, IServiceResult result)
        {
            // Success: 200 OK
            if (IsSuccess(result.ResultCode))
            {
                return controller.Ok(result);
            }

            // Unauthorized: 401
            if (IsUnauthorized(result.ResultCode))
            {
                return controller.Unauthorized(result);
            }

            // Forbidden: 403
            if (IsForbidden(result.ResultCode))
            {
                return controller.StatusCode(StatusCodes.Status403Forbidden, result);
            }

            // Not Found: 404
            if (IsNotFound(result.ResultCode))
            {
                return controller.NotFound(result);
            }

            // Warning: 400 Bad Request
            if (IsWarning(result.ResultCode))
            {
                return controller.BadRequest(result);
            }

            // Fail: 500 Internal Server Error
            return controller.StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        #region Mapping Logic

        private static bool IsSuccess(string code) => 
            code.Contains(".Success", StringComparison.OrdinalIgnoreCase);

        private static bool IsWarning(string code) => 
            code.Contains(".Warning", StringComparison.OrdinalIgnoreCase);
        
        private static bool IsFail(string code) =>
            code.Contains(".Fail", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnauthorized(string code)
        {
            return code == ResultCodeConst.Auth_Warning0002  // Invalid token
                || code == ResultCodeConst.Auth_Warning0013; // Auth required
        }

        private static bool IsForbidden(string code)
        {
            return code == ResultCodeConst.Auth_Warning0001  // Account banned
                || code == ResultCodeConst.User_Warning0002  // Not a rescuer (Role check)
                || code == ResultCodeConst.SYS_Warning0007;  // Not authorized/not owner
        }

        private static bool IsNotFound(string code)
        {
            return code == ResultCodeConst.SYS_Warning0002      // General Not Found
                || code == ResultCodeConst.Medical_Warning0001; // No Facility Found
        }

        #endregion
    }
}