using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MySqlConnector;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.DTOs.Posts;
using Xunit;

namespace SocialSystem.Api.Tests;

public sealed class PostsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Client, long Id)> Account()
    {
        var client = factory.CreateClient();
        var username = "post_" + Guid.NewGuid().ToString("N")[..20];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password, nickname = "Post test" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = (await registration.Content.ReadFromJsonAsync<UserResponse>())!;
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, user.Id);
    }

    private static async Task<PostResponse> Create(HttpClient client, string content = "hello social system")
    {
        using var response = await client.PostAsJsonAsync("/api/posts", new { content, userId = long.MaxValue });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        return (await response.Content.ReadFromJsonAsync<PostResponse>())!;
    }

    private static async Task<PostResponse> Find(HttpClient client, long id)
    {
        var list = (await client.GetFromJsonAsync<PostListResponse>("/api/posts?pageSize=100"))!;
        return Assert.Single(list.Items, x => x.Id == id);
    }

    [Theory]
    [InlineData("POST", "/api/posts")]
    [InlineData("GET", "/api/posts")]
    [InlineData("DELETE", "/api/posts/1")]
    [InlineData("POST", "/api/posts/1/comments")]
    [InlineData("GET", "/api/posts/1/comments")]
    [InlineData("POST", "/api/posts/1/like")]
    [InlineData("DELETE", "/api/posts/1/like")]
    public async Task All_endpoints_require_JWT(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { content = "hello" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_checks_author_counts_like_state_and_cascade_deletion()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        using (a) using (b)
        {
            var post = await Create(a, "中文 😀");
            Assert.Equal(aId, post.Author.Id);
            Assert.Equal(DateTimeKind.Utc, post.CreatedAt.Kind);
            Assert.Equal("中文 😀", post.Content);
            using var comment = await b.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "good" });
            Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
            var savedComment = (await comment.Content.ReadFromJsonAsync<CommentResponse>())!;
            Assert.Equal(bId, savedComment.Author.Id);
            Assert.Equal(post.Id, savedComment.PostId);
            using var liked = await b.PostAsync($"/api/posts/{post.Id}/like", null);
            using var likedAgain = await b.PostAsync($"/api/posts/{post.Id}/like", null);
            Assert.Equal(HttpStatusCode.NoContent, liked.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, likedAgain.StatusCode);
            var bView = await Find(b, post.Id);
            Assert.Equal(1, bView.CommentCount);
            Assert.Equal(1, bView.LikeCount);
            Assert.True(bView.IsLikedByMe);
            Assert.False((await Find(a, post.Id)).IsLikedByMe);
            using var forbidden = await b.DeleteAsync($"/api/posts/{post.Id}");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var unliked = await b.DeleteAsync($"/api/posts/{post.Id}/like");
            using var unlikedAgain = await b.DeleteAsync($"/api/posts/{post.Id}/like");
            Assert.Equal(HttpStatusCode.NoContent, unliked.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, unlikedAgain.StatusCode);
            Assert.Equal(0, (await Find(b, post.Id)).LikeCount);
            using var reliked = await b.PostAsync($"/api/posts/{post.Id}/like", null);
            Assert.Equal(HttpStatusCode.NoContent, reliked.StatusCode);
            using var deleted = await a.DeleteAsync($"/api/posts/{post.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            await using var connection = new MySqlConnection(factory.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT COUNT(*) FROM posts WHERE id=@id) + (SELECT COUNT(*) FROM comments WHERE post_id=@id) + (SELECT COUNT(*) FROM likes WHERE post_id=@id)";
            command.Parameters.AddWithValue("@id", post.Id);
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            using var deletedComment = await b.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "late" });
            using var deletedLike = await b.PostAsync($"/api/posts/{post.Id}/like", null);
            using var deletedUnlike = await b.DeleteAsync($"/api/posts/{post.Id}/like");
            using var deletedAgain = await a.DeleteAsync($"/api/posts/{post.Id}");
            Assert.Equal(HttpStatusCode.NotFound, deletedComment.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deletedLike.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deletedUnlike.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deletedAgain.StatusCode);
        }
    }

    [Fact]
    public async Task Concurrent_likes_are_unique_and_different_users_count_separately()
    {
        var (a, _) = await Account();
        var (b, _) = await Account();
        using (a) using (b)
        {
            var post = await Create(a);
            var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => b.PostAsync($"/api/posts/{post.Id}/like", null)));
            foreach (var response in responses)
            {
                using (response) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }
            Assert.Equal(1, (await Find(a, post.Id)).LikeCount);
            using var authorLike = await a.PostAsync($"/api/posts/{post.Id}/like", null);
            Assert.Equal(HttpStatusCode.NoContent, authorLike.StatusCode);
            Assert.Equal(2, (await Find(a, post.Id)).LikeCount);
            using var removed = await b.DeleteAsync($"/api/posts/{post.Id}/like");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            var authorView = await Find(a, post.Id);
            Assert.Equal(1, authorView.LikeCount);
            Assert.True(authorView.IsLikedByMe);
        }
    }

    [Fact]
    public async Task Validation_rejects_blank_oversized_content_and_invalid_ids()
    {
        var (client, _) = await Account();
        using (client)
        {
            foreach (var content in new string?[] { null, "", " \t\n", new string('x', 2001) })
            {
                using var invalid = await client.PostAsJsonAsync("/api/posts", new { content });
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            var post = await Create(client, new string('x', 2000));
            foreach (var content in new string?[] { null, "", " \t\n", new string('x', 501) })
            {
                using var invalid = await client.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content });
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            using var valid = await client.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = new string('x', 500) });
            Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
            using var invalidPage = await client.GetAsync("/api/posts?page=0&pageSize=101");
            using var invalidId = await client.DeleteAsync("/api/posts/0");
            using var missing = await client.PostAsync($"/api/posts/{long.MaxValue}/like", null);
            Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    [Fact]
    public async Task List_returns_safe_authors_and_stable_newest_first_pagination()
    {
        var (client, id) = await Account();
        using (client)
        {
            var first = await Create(client);
            var second = await Create(client);
            await using var connection = new MySqlConnection(factory.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE posts SET created_at='2200-01-01 00:00:00' WHERE id IN (@a,@b)";
            command.Parameters.AddWithValue("@a", first.Id);
            command.Parameters.AddWithValue("@b", second.Id);
            Assert.Equal(2, await command.ExecuteNonQueryAsync());
            var page1 = (await client.GetFromJsonAsync<PostListResponse>("/api/posts?page=1&pageSize=1"))!;
            var page2 = (await client.GetFromJsonAsync<PostListResponse>("/api/posts?page=2&pageSize=1"))!;
            Assert.Equal(second.Id, Assert.Single(page1.Items).Id);
            Assert.Equal(first.Id, Assert.Single(page2.Items).Id);
            Assert.Equal(id, page1.Items[0].Author.Id);
            Assert.True(page1.Total >= 2);
            using var raw = await client.GetAsync("/api/posts");
            Assert.DoesNotContain("password", await raw.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Comments_are_visible_across_users_with_safe_authors_and_paging()
    {
        var (a, _) = await Account(); var (b, bId) = await Account();
        using (a) using (b)
        {
            var post = await Create(a);
            var other = await Create(a);
            var empty = (await b.GetFromJsonAsync<CommentListResponse>($"/api/posts/{post.Id}/comments"))!;
            Assert.Empty(empty.Items); Assert.Equal(0, empty.Total);
            var saved = new List<CommentResponse>();
            foreach (var content in new[] { "第一条中文评论", "<b>纯文本</b>\n第二行", new string('长', 500) })
            {
                using var result = await b.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content });
                Assert.Equal(HttpStatusCode.Created, result.StatusCode);
                saved.Add((await result.Content.ReadFromJsonAsync<CommentResponse>())!);
            }
            using var unrelated = await a.PostAsJsonAsync($"/api/posts/{other.Id}/comments", new { content = "other post" });
            Assert.Equal(HttpStatusCode.Created, unrelated.StatusCode);
            // Equal timestamps still have deterministic ID ordering.
            await using var connection = new MySqlConnection(factory.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE comments SET created_at='2026-01-01 00:00:00' WHERE post_id=@id";
            command.Parameters.AddWithValue("@id", post.Id);
            Assert.Equal(3, await command.ExecuteNonQueryAsync());
            var first = (await a.GetFromJsonAsync<CommentListResponse>($"/api/posts/{post.Id}/comments?pageSize=2"))!;
            var second = (await a.GetFromJsonAsync<CommentListResponse>($"/api/posts/{post.Id}/comments?page=2&pageSize=2"))!;
            Assert.Equal(3, first.Total);
            Assert.Equal(new[] { saved[2].Id, saved[1].Id }, first.Items.Select(x => x.Id));
            Assert.Equal(saved[0].Id, Assert.Single(second.Items).Id);
            var identity = (await b.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
            Assert.All(first.Items, x =>
            {
                Assert.Equal(post.Id, x.PostId); Assert.Equal(bId, x.Author.Id);
                Assert.Equal(identity.Username, x.Author.Username); Assert.Equal(identity.Nickname, x.Author.Nickname);
                Assert.Equal(DateTimeKind.Utc, x.CreatedAt.Kind);
            });
            var bView = (await b.GetFromJsonAsync<CommentListResponse>($"/api/posts/{post.Id}/comments"))!;
            Assert.Equal(saved.AsEnumerable().Reverse().Select(x => x.Id), bView.Items.Select(x => x.Id));
            Assert.Equal(3, (await Find(a, post.Id)).CommentCount);
            Assert.Empty((await a.GetFromJsonAsync<CommentListResponse>($"/api/posts/{post.Id}/comments?page=3&pageSize=2"))!.Items);
            using var raw = await a.GetAsync($"/api/posts/{post.Id}/comments");
            Assert.DoesNotContain("password", await raw.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Comment_queries_validate_ids_and_pagination()
    {
        var (client, _) = await Account();
        using (client)
        {
            var post = await Create(client);
            foreach (var query in new[] { "?page=0", "?page=1000001", "?pageSize=0", "?pageSize=101" })
            {
                using var response = await client.GetAsync($"/api/posts/{post.Id}/comments" + query);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            using var badId = await client.GetAsync("/api/posts/0/comments");
            using var missing = await client.GetAsync($"/api/posts/{long.MaxValue}/comments");
            Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    [Fact]
    public async Task Deleted_posts_do_not_expose_stale_comments()
    {
        var (a, _) = await Account(); var (b, _) = await Account();
        using (a) using (b)
        {
            var post = await Create(a);
            using var comment = await b.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "before deletion" });
            Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
            using var deleted = await a.DeleteAsync($"/api/posts/{post.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var queried = await b.GetAsync($"/api/posts/{post.Id}/comments");
            Assert.Equal(HttpStatusCode.NotFound, queried.StatusCode);
        }
    }
}
