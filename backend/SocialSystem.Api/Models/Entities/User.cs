namespace SocialSystem.Api.Models.Entities;

public sealed class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AvatarKey { get; set; } = "default";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

