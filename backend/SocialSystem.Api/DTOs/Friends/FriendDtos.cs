using System.ComponentModel.DataAnnotations;
namespace SocialSystem.Api.DTOs.Friends;
public sealed class SendFriendRequest
{
    [Range(1, long.MaxValue)]
    public long ReceiverId { get; init; }
}
public sealed record FriendRequestResponse(long RequestId, long SenderId, long ReceiverId, string Status);
public sealed record FriendResponse(long FriendId, string Username, string Nickname, string AvatarKey, DateTime CreatedAt);
public sealed record FriendListResponse(IReadOnlyList<FriendResponse> Items, int Page, int PageSize, int Total);

