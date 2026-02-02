using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Auth;
using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService<AuthUserDto> _authService;
        public AuthController(
            IAuthService<AuthUserDto> authService)
        {
            _authService = authService;
        }
    

        [HttpPost(APIRoute.Auth.SignInWithPassword, Name = nameof(SignInWithPasswordAsync))]
        public async Task<IActionResult> SignInWithPasswordAsync([FromBody] SignInWithPasswordRequest req)
        {
            var result = await _authService.SignInWithPasswordAsync(req.ToAuthenticatedUser());
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.SignInWithGoogle, Name = nameof(SignInWithGoogleAsync))]
        public async Task<IActionResult> SignInWithGoogleAsync([FromBody] SignInWithGoogleRequest req)
        {
            var result = await _authService.SignInWithGoogleAsync(req.Credential);
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.SignUp, Name = nameof(SignUpAsync))]
        public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest req)
        {
            var result = await _authService.SignUpAsync(req.ToAuthenticatedUser());
            return this.ToIActionResult(result);
        }
    }
}