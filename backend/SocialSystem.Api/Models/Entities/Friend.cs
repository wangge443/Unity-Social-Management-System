namespace SocialSystem.Api.Models.Entities;

public sealed class Friend
{
    public long Id { get; set; }
    public long UserLowId { get; set; }
    public long UserHighId { get; set; }
    public DateTime CreatedAt { get; set; }
    public User LowUser { get; set; } = null!;
    public User HighUser { get; set; } = null!;
}

