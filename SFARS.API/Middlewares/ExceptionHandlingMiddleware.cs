using SFARS.Application.Common;
using SFARS.Application.Services;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Middlewares
{
    /// <summary>
    /// Exception handling middleware that catches all exceptions
    /// and returns standardized ServiceResult responses
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext httpContext,
            ISystemMessageService msgService)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                // If response has already started, we cannot modify it
                if (httpContext.Response.HasStarted)
                {
                    throw;
                }

                await HandleExceptionAsync(httpContext, ex, msgService);
            }
        }

        private async Task HandleExceptionAsync(
            HttpContext httpContext,
            Exception ex,
            ISystemMessageService msgService)
        {
            // Log the exception with full details
            _logger.LogError(ex, 
                "Unhandled exception occurred. Path: {Path}, Method: {Method}", 
                httpContext.Request.Path, 
                httpContext.Request.Method);

            // Create ServiceResult for all unhandled exceptions
            var result = new ServiceResult(
                ResultCodeConst.SYS_Fail0001,
                await msgService.GetMessageAsync(ResultCodeConst.SYS_Fail0001)
            );

            // Set response metadata
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/json";

            // Write ServiceResult as JSON
            await httpContext.Response.WriteAsJsonAsync(result);
        }
    }
}