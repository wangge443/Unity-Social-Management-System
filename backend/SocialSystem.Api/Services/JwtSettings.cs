using Microsoft.IdentityModel.Tokens;
namespace SocialSystem.Api.Services;
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "SocialSystem.Api";
    public string Audience { get; set; } = "SocialSystem.Client";
    public int ExpirationMinutes { get; set; } = 60;
    public string SigningKey { get; set; } = "";
    public bool IsValid()
    {
        try { return Convert.FromBase64String(SigningKey).Length >= 32
            && !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(Audience)
            && ExpirationMinutes is >= 1 and <= 1440; }
        catch (FormatException) { return false; }
    }
    public SymmetricSecurityKey SecurityKey() => new(Convert.FromBase64String(SigningKey));
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = SecurityKey(),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero, NameClaimType = "unique_name"
    };
}

