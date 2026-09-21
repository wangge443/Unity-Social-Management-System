namespace SocialSystem.Api.Models.Entities;

public sealed class Post
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

