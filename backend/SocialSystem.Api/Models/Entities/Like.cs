namespace SocialSystem.Api.Models.Entities;

public sealed class Like
{
    public long PostId { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Post Post { get; set; } = null!;
    public User User { get; set; } = null!;
}

