using System.Data;
using Microsoft.EntityFrameworkCore;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs.Messages;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Services;

public sealed class MessageOperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class MessageService(SocialDbContext db)
{
    private static MessageOperationException Error(int status, string code, string message) => new(status, code, message);

    public async Task<MessageResponse> SendAsync(long userId, SendMessageRequest input, CancellationToken ct)
    {
        if (userId == input.ReceiverId) throw Error(400, "self_message", "不能给自己发送消息");
        var low = Math.Min(userId, input.ReceiverId);
        var high = Math.Max(userId, input.ReceiverId);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Same lock order as FriendService: a concurrent unfriend cannot slip between
        // the friendship check and the message insert. No Friend module changes needed.
        foreach (var id in new[] { low, high })
        {
            var users = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            if (users.Count == 0)
                throw id == userId ? Error(401, "user_not_found", "当前用户不存在")
                    : Error(404, "user_not_found", "目标用户不存在");
        }
        if (!await db.Friends.AnyAsync(x => x.UserLowId == low && x.UserHighId == high, ct))
            throw Error(403, "not_friends", "只有好友之间可以发送消息");
        var message = new Message { SenderId = userId, ReceiverId = input.ReceiverId, Content = input.Content };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(message.Id, message.SenderId, message.ReceiverId, message.Content,
            DateTime.SpecifyKind(message.CreatedAt, DateTimeKind.Utc));
    }

    public async Task<IReadOnlyList<MessageResponse>> HistoryAsync(long userId, long friendId, CancellationToken ct)
    {
        if (userId == friendId) throw Error(400, "self_message", "不能查询与自己的私聊");
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            throw Error(401, "user_not_found", "当前用户不存在");
        if (!await db.Users.AnyAsync(x => x.Id == friendId, ct))
            throw Error(404, "user_not_found", "目标用户不存在");
        // Retain access to one's own history after unfriend; never accept a sender ID from the client.
        return await db.Messages.AsNoTracking()
            .Where(x => (x.SenderId == userId && x.ReceiverId == friendId)
                || (x.SenderId == friendId && x.ReceiverId == userId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new MessageResponse(x.Id, x.SenderId, x.ReceiverId, x.Content,
                DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc))).ToListAsync(ct);
    }
}

