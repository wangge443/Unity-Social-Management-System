using System.Data;
using Microsoft.EntityFrameworkCore;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs.Posts;
using SocialSystem.Api.Models.Entities;

namespace SocialSystem.Api.Services;

public sealed class PostOperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class PostService(SocialDbContext db)
{
    private async Task<User> CurrentUserAsync(long id, CancellationToken ct) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new PostOperationException(401, "user_not_found", "当前用户不存在");

    private static PostAuthorResponse Author(User user) => new(user.Id, user.Username, user.Nickname, user.AvatarKey);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Called inside a transaction: serialize comments/likes/deletion on the same post.
    private async Task<Post> LockPostAsync(long id, CancellationToken ct)
    {
        var rows = await db.Posts.FromSqlInterpolated($"SELECT * FROM posts WHERE id = {id} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new PostOperationException(404, "post_not_found", "动态不存在");
    }

    public async Task<PostResponse> CreateAsync(long userId, CreatePostRequest input, CancellationToken ct)
    {
        var user = await CurrentUserAsync(userId, ct);
        var post = new Post { UserId = userId, Content = input.Content };
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return new(post.Id, Author(user), post.Content, Utc(post.CreatedAt), 0, 0, false);
    }

    public async Task<PostListResponse> ListAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        await CurrentUserAsync(userId, ct);
        var total = await db.Posts.CountAsync(ct);
        var items = await db.Posts.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new PostResponse(x.Id,
                new PostAuthorResponse(x.User.Id, x.User.Username, x.User.Nickname, x.User.AvatarKey),
                x.Content, DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
                db.Comments.Count(c => c.PostId == x.Id),
                db.Likes.Count(l => l.PostId == x.Id),
                db.Likes.Any(l => l.PostId == x.Id && l.UserId == userId))).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    public async Task DeleteAsync(long userId, long id, CancellationToken ct)
    {
        await CurrentUserAsync(userId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var post = await LockPostAsync(id, ct);
        if (post.UserId != userId)
            throw new PostOperationException(403, "not_post_author", "只能删除自己的动态");
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        // Existing foreign keys cascade deletion to comments and likes.
        await transaction.CommitAsync(ct);
    }

    public async Task<CommentResponse> CommentAsync(long userId, long id, CreateCommentRequest input, CancellationToken ct)
    {
        var user = await CurrentUserAsync(userId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockPostAsync(id, ct);
        var comment = new Comment { PostId = id, UserId = userId, Content = input.Content };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(comment.Id, id, Author(user), comment.Content, Utc(comment.CreatedAt));
    }

    public async Task SetLikeAsync(long userId, long id, bool liked, CancellationToken ct)
    {
        await CurrentUserAsync(userId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockPostAsync(id, ct);
        var existing = await db.Likes.SingleOrDefaultAsync(x => x.PostId == id && x.UserId == userId, ct);
        if (liked && existing is null) db.Likes.Add(new Like { PostId = id, UserId = userId });
        if (!liked && existing is not null) db.Likes.Remove(existing);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
