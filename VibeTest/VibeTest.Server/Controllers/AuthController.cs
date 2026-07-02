using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VibeTest.Server.Configuration;
using VibeTest.Server.Extensions;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Server.Services;

namespace VibeTest.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthRegisterRefresh)]
    public Task<AuthResponse> Register([FromBody] RegisterRequest request) =>
        authService.RegisterAsync(request);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    public Task<AuthResponse> Login([FromBody] LoginRequest request) =>
        authService.LoginAsync(request);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthRegisterRefresh)]
    public Task<TokenRefreshResponse> Refresh([FromBody] RefreshTokenRequest request) =>
        authService.RefreshAsync(request);

    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.GlobalApi)]
    public Task<UserDto> Me() =>
        authService.GetMeAsync(User.GetUserId());
}
