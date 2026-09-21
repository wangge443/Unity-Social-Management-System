using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.Models.Entities;
namespace SocialSystem.Api.Services;
public sealed class TokenService(IOptions<JwtSettings> options, TimeProvider clock)
{
    public LoginResponse Issue(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.ExpirationMinutes);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
             new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))],
            now, expires, new SigningCredentials(settings.SecurityKey(), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires, UserResponse.From(user));
    }
}

