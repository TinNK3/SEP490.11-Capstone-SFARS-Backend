using Microsoft.AspNetCore.Authorization;
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

        [HttpPost(APIRoute.Auth.SignIn, Name = nameof(SignInAsync))]
        public async Task<IActionResult> SignInAsync([FromBody] SignInRequest req)
        {
            var result = await _authService.SignInAsync(req.Email);
            return this.ToIActionResult(result);
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

        [HttpPost(APIRoute.Auth.SignInWithOtp, Name = nameof(SignInWithOtpAsync))]
        public async Task<IActionResult> SignInWithOtpAsync([FromBody] SignInWithOtpRequest req)
        {
            var result = await _authService.SignInWithOtpAsync(req.Otp, req.ToAuthenticatedUser());
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.SignUp, Name = nameof(SignUpAsync))]
        public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest req)
        {
            var result = await _authService.SignUpAsync(req.ToAuthenticatedUser());
            return this.ToIActionResult(result);
        }



        [HttpPost(APIRoute.Auth.ForgotPassword, Name = nameof(ForgotPasswordAsync))]
        public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordRequest req)
        {
            var result = await _authService.ForgotPasswordAsync(req.Email);
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.ResetPassword, Name = nameof(ResetPasswordAsync))]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest req)
        {
            var result = await _authService.ResetPasswordAsync(req.Email, req.Otp, req.NewPassword);
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.SendOtp, Name = nameof(SendOtpAsync))]
        public async Task<IActionResult> SendOtpAsync([FromBody] SendOtpRequest req)
        {
            var result = await _authService.SendOtpAsync(req.Email, req.Type);
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.VerifyOtp, Name = nameof(VerifyOtpAsync))]
        public async Task<IActionResult> VerifyOtpAsync([FromBody] VerifyOtpRequest req)
        {
            var result = await _authService.VerifyOtpAsync(req.Email, req.Otp, req.Type);
            return this.ToIActionResult(result);
        }

        [HttpPost(APIRoute.Auth.RefreshToken, Name = nameof(RefreshTokenAsync))]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest req)
        {
            var result = await _authService.RefreshTokenAsync(req.RefreshToken, req.AccessToken);
            return this.ToIActionResult(result);
        }

        [Authorize]
        [HttpPost(APIRoute.Auth.SignOut, Name = nameof(SignOutAsync))]
        public async Task<IActionResult> SignOutAsync()
        {
            var userId = User.GetUserId();
            
            // Extract access token from Authorization header
            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            var result = await _authService.SignOutAsync(userId, accessToken);
            return this.ToIActionResult(result);
        }
    }
}