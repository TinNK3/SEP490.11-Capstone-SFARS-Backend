using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Auth;
using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller
{
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService<AuthenticateUserDto> _authenticationService;
        public AuthenticationController(
            IAuthenticationService<AuthenticateUserDto> authenticationService)
        {
            _authenticationService = authenticationService;
        }
    

        [HttpPost(APIRoute.Authentication.SignInWithPassword, Name = nameof(SignInWithPasswordAsync))]
        public async Task<IActionResult> SignInWithPasswordAsync([FromBody] SignInWithPasswordRequest req)
        {
            return Ok(await _authenticationService.SignInWithPasswordAsync(req.ToAuthenticatedUser()));
        }

        //[HttpPost(APIRoute.Authentication.SignUp, Name = nameof(SignUpAsync))]
        //public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest req)
        //{
        //    return Ok(await _authenticationService.SignUpAsync(req.ToAuthenticatedUser()));
        //}
    }
}
