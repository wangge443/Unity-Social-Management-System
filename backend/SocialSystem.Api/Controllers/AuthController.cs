using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.Services;
namespace SocialSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, SocialDbContext db) : ControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request, ct)); }
        catch (DuplicateUsernameException)
        {
            return Problem(statusCode: 409, title: "用户名已存在", extensions: new Dictionary<string, object?> { ["code"] = "username_taken" });
        }
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await auth.LoginAsync(request, ct);
        if (response is null)
            return Problem(statusCode: 401, title: "用户名或密码错误", extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" });
        return Ok(response);
    }

    // Authentication verification only; not a profile editing feature.
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)) return Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return user is null ? Unauthorized() : Ok(UserResponse.From(user));
    }
}

