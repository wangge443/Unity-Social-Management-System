using System.ComponentModel.DataAnnotations;
namespace SocialSystem.Api.DTOs.Auth;
public sealed class RegisterRequest
{
    [Required, RegularExpression(@"\A[A-Za-z0-9_]{3,32}\z")]
    public string Username { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = "";
    [Required, StringLength(32)]
    public string Nickname { get; init; } = "";
}

