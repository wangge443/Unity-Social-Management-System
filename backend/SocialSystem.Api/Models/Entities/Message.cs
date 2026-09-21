namespace SocialSystem.Api.Models.Entities;

public sealed class Message
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}

