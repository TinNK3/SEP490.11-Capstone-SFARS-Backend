using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SFARS.Application.Common;
using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.API.Extensions
{
    public static class ControllerExtensions
    {
        public static IActionResult ToIActionResult(this ControllerBase controller, IServiceResult result)
        {
            if (IsSuccess(result.ResultCode))
            {
                return controller.Ok(result);
            }

            int statusCode;
            string title;
            string type;

            if (IsUnauthorized(result.ResultCode))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Unauthorized";
                type = "https://tools.ietf.org/html/rfc7235#section-3.1";
            }
            else if (IsForbidden(result.ResultCode))
            {
                statusCode = StatusCodes.Status403Forbidden;
                title = "Forbidden";
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
            }
            else if (IsNotFound(result.ResultCode))
            {
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
            }
            else if (IsWarning(result.ResultCode))
            {
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
            }
            else // Fail / Internal Error
            {
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Internal Server Error";
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
            }

            // Create standard ProblemDetails
            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = type,
                Detail = result.Message,
                Instance = controller.HttpContext.Request.Path
            };

            // Add custom extension for internal error code (optional but helpful)
            problemDetails.Extensions.Add("resultCode", result.ResultCode);

            // If there are validation errors in Data, add them
            if (result.Data is IDictionary<string, string[]> validationErrors)
            {
                 // Create ValidationProblemDetails if we have specific validation errors
                 var validationProblem = new ValidationProblemDetails(validationErrors)
                 {
                     Status = statusCode,
                     Title = title,
                     Type = type,
                     Detail = result.Message,
                     Instance = controller.HttpContext.Request.Path
                 };
                 validationProblem.Extensions.Add("resultCode", result.ResultCode);
                 return new ObjectResult(validationProblem) { StatusCode = statusCode };
            }

            return new ObjectResult(problemDetails)
            {
                StatusCode = statusCode
            };
        }

        #region Mapping Logic

        private static bool IsSuccess(string code) => 
            code.Contains("Success", StringComparison.OrdinalIgnoreCase);

        private static bool IsWarning(string code) => 
            code.Contains("Warning", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnauthorized(string code)
        {
            return code == ResultCodeConst.Auth_Warning0002  // Invalid token
                || code == ResultCodeConst.Auth_Warning0013; // Auth required
        }

        private static bool IsForbidden(string code)
        {
            return code == ResultCodeConst.Auth_Warning0001  // Account banned
                || code == ResultCodeConst.User_Warning0002; // Not a rescuer (Role check)
        }

        private static bool IsNotFound(string code)
        {
            return code == ResultCodeConst.SYS_Warning0002      // General Not Found
                || code == ResultCodeConst.Medical_Warning0001; // No Facility Found
        }

        #endregion
    }
}