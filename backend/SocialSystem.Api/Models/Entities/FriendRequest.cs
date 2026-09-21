namespace SocialSystem.Api.Models.Entities;

public sealed class FriendRequest
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public FriendRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
    public long? PendingLowId { get; private set; }
    public long? PendingHighId { get; private set; }
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}

