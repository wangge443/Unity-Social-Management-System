using System.ComponentModel.DataAnnotations;
namespace SocialSystem.Api.DTOs.Messages;

public sealed class SendMessageRequest
{
    [Range(1, long.MaxValue)]
    public long ReceiverId { get; init; }
    [Required, StringLength(2000)]
    public string Content { get; init; } = "";
}

public sealed record MessageResponse(long Id, long SenderId, long ReceiverId, string Content, DateTime CreatedAt);

