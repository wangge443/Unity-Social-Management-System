using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs.Friends;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Services;

public sealed class FriendOperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class FriendService(SocialDbContext db)
{
    private static FriendOperationException Error(int status, string code, string message) => new(status, code, message);

    // Every pair mutation takes the same locks, in ascending user ID order.
    // This also serializes simultaneous requests, acceptance and removal.
    private async Task LockPairAsync(long currentId, long otherId, CancellationToken ct)
    {
        if (currentId == otherId) throw Error(400, "self_friendship", "不能添加或删除自己");
        foreach (var id in new[] { Math.Min(currentId, otherId), Math.Max(currentId, otherId) })
        {
            var rows = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            if (rows.Count == 0)
                throw id == currentId ? Error(401, "user_not_found", "当前用户不存在")
                    : Error(404, "user_not_found", "目标用户不存在");
        }
    }

    private IQueryable<Friend> Pair(long a, long b)
    {
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        return db.Friends.Where(x => x.UserLowId == low && x.UserHighId == high);
    }


    public async Task<UserSearchListResponse> SearchUsersAsync(long userId, string keyword, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            throw Error(401, "user_not_found", "当前用户不存在");
        keyword = keyword.Trim();
        var query = db.Users.AsNoTracking().Where(x => x.Id != userId);
        // usernames use ASCII; non-ASCII keywords can only match nicknames.
        query = keyword.All(c => c <= 127)
            ? query.Where(x => x.Username.Contains(keyword) || x.Nickname.Contains(keyword))
            : query.Where(x => x.Nickname.Contains(keyword));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Username).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new UserSearchResponse(x.Id, x.Username, x.Nickname,
                db.Friends.Any(f => (f.UserLowId == userId && f.UserHighId == x.Id) ||
                    (f.UserHighId == userId && f.UserLowId == x.Id)) ? "friends" :
                db.FriendRequests.Any(r => r.SenderId == userId && r.ReceiverId == x.Id &&
                    r.Status == FriendRequestStatus.Pending) ? "outgoing" :
                db.FriendRequests.Any(r => r.ReceiverId == userId && r.SenderId == x.Id &&
                    r.Status == FriendRequestStatus.Pending) ? "incoming" : "none"))
            .ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    public async Task<FriendRequestResponse> SendAsync(long userId, long receiverId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockPairAsync(userId, receiverId, ct);
        if (await Pair(userId, receiverId).AnyAsync(ct))
            throw Error(409, "already_friends", "双方已经是好友");
        if (await db.FriendRequests.AnyAsync(x => x.Status == FriendRequestStatus.Pending &&
            ((x.SenderId == userId && x.ReceiverId == receiverId) || (x.SenderId == receiverId && x.ReceiverId == userId)), ct))
            throw Error(409, "request_pending", "双方已有待处理好友申请");
        var request = new FriendRequest { SenderId = userId, ReceiverId = receiverId };
        db.FriendRequests.Add(request);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 })
        {
            throw Error(409, "request_pending", "双方已有待处理好友申请");
        }
        await transaction.CommitAsync(ct);
        return new(request.Id, userId, receiverId, "pending");
    }

    public async Task<FriendRequestResponse> AcceptAsync(long userId, long requestId, CancellationToken ct)
    {
        var snapshot = await db.FriendRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw Error(404, "request_not_found", "好友申请不存在");
        if (snapshot.ReceiverId != userId)
            throw Error(403, "not_request_receiver", "只有接收者可以接受申请");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockPairAsync(userId, snapshot.SenderId, ct);
        var request = await db.FriendRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw Error(404, "request_not_found", "好友申请不存在");
        if (request.Status != FriendRequestStatus.Pending)
            throw Error(409, "request_handled", "好友申请已处理");
        if (await Pair(userId, request.SenderId).AnyAsync(ct))
            throw Error(409, "already_friends", "双方已经是好友");

        request.Status = FriendRequestStatus.Accepted;
        // Use the DB clock to satisfy handled_at >= created_at even if host clocks differ.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT UTC_TIMESTAMP(6)";
        var now = Convert.ToDateTime(await command.ExecuteScalarAsync(ct));
        request.HandledAt = now < request.CreatedAt ? request.CreatedAt : now;
        db.Friends.Add(new Friend { UserLowId = Math.Min(userId, request.SenderId), UserHighId = Math.Max(userId, request.SenderId) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 })
        {
            throw Error(409, "already_friends", "双方已经是好友");
        }
        await transaction.CommitAsync(ct);
        return new(request.Id, request.SenderId, request.ReceiverId, "accepted");
    }

    public async Task<IncomingFriendRequestListResponse> IncomingAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            throw Error(401, "user_not_found", "当前用户不存在");
        var query = db.FriendRequests.AsNoTracking()
            .Where(x => x.ReceiverId == userId && x.Status == FriendRequestStatus.Pending);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new IncomingFriendRequestResponse(x.Id, x.SenderId,
                x.Sender.Username, x.Sender.Nickname, DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)))
            .ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    public async Task<FriendRequestResponse> RejectAsync(long userId, long requestId, CancellationToken ct)
    {
        var snapshot = await db.FriendRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw Error(404, "request_not_found", "好友申请不存在");
        if (snapshot.ReceiverId != userId)
            throw Error(403, "not_request_receiver", "只有接收者可以拒绝申请");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Use the same ordered user locks as acceptance; concurrent accept/reject has one winner.
        await LockPairAsync(userId, snapshot.SenderId, ct);
        var request = await db.FriendRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw Error(404, "request_not_found", "好友申请不存在");
        if (request.ReceiverId != userId)
            throw Error(403, "not_request_receiver", "只有接收者可以拒绝申请");
        if (request.Status != FriendRequestStatus.Pending)
            throw Error(409, "request_handled", "好友申请已处理");
        request.Status = FriendRequestStatus.Rejected;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT UTC_TIMESTAMP(6)";
        var now = Convert.ToDateTime(await command.ExecuteScalarAsync(ct));
        request.HandledAt = now < request.CreatedAt ? request.CreatedAt : now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(request.Id, request.SenderId, request.ReceiverId, "rejected");
    }


    public async Task DeleteAsync(long userId, long friendId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockPairAsync(userId, friendId, ct);
        // Deleting an already absent relationship succeeds without touching request history or messages.
        await Pair(userId, friendId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<FriendListResponse> ListAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            throw Error(401, "user_not_found", "当前用户不存在");
        var query = db.Friends.AsNoTracking().Where(x => x.UserLowId == userId || x.UserHighId == userId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new FriendResponse(
                x.UserLowId == userId ? x.UserHighId : x.UserLowId,
                x.UserLowId == userId ? x.HighUser.Username : x.LowUser.Username,
                x.UserLowId == userId ? x.HighUser.Nickname : x.LowUser.Nickname,
                x.UserLowId == userId ? x.HighUser.AvatarKey : x.LowUser.AvatarKey,
                x.CreatedAt)).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }
}

