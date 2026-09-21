using System.ComponentModel.DataAnnotations;
namespace SocialSystem.Api.DTOs.Posts;

public sealed class CreatePostRequest
{
    [Required, StringLength(2000)]
    public string Content { get; init; } = "";
}
public sealed class CreateCommentRequest
{
    [Required, StringLength(500)]
    public string Content { get; init; } = "";
}
public sealed record PostAuthorResponse(long Id, string Username, string Nickname, string AvatarKey);
public sealed record PostResponse(long Id, PostAuthorResponse Author, string Content, DateTime CreatedAt,
    int CommentCount, int LikeCount, bool IsLikedByMe);
public sealed record PostListResponse(IReadOnlyList<PostResponse> Items, int Page, int PageSize, int Total);
public sealed record CommentResponse(long Id, long PostId, PostAuthorResponse Author, string Content, DateTime CreatedAt);
