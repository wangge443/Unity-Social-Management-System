using SocialSystem.Api.Models.Entities;
namespace SocialSystem.Api.DTOs.Auth;
public sealed record UserResponse(long Id, string Username, string Nickname)
{
    public static UserResponse From(User user) => new(user.Id, user.Username, user.Nickname);
}
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, UserResponse User);

