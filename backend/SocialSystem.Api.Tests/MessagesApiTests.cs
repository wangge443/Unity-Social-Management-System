using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MySqlConnector;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.DTOs.Friends;
using SocialSystem.Api.DTOs.Messages;
using Xunit;

namespace SocialSystem.Api.Tests;

public sealed class MessagesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Client, long Id)> Account()
    {
        var client = factory.CreateClient();
        var username = "msg_" + Guid.NewGuid().ToString("N")[..20];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password, nickname = "Message test" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = (await registration.Content.ReadFromJsonAsync<UserResponse>())!;
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, user.Id);
    }

    private static async Task Befriend(HttpClient sender, HttpClient receiver, long receiverId)
    {
        using var response = await sender.PostAsJsonAsync("/api/friends/request", new { receiverId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var request = (await response.Content.ReadFromJsonAsync<FriendRequestResponse>())!;
        using var accepted = await receiver.PostAsync($"/api/friends/accept/{request.RequestId}", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    private static async Task<MessageResponse> Send(HttpClient client, long receiverId, string content)
    {
        using var response = await client.PostAsJsonAsync("/api/messages/send", new { receiverId, content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    [Theory]
    [InlineData("POST", "/api/messages/send")]
    [InlineData("GET", "/api/messages/1")]
    public async Task Both_endpoints_require_JWT(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { receiverId = 1, content = "hello" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Two_way_history_is_private_and_sorted_by_time_then_id()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        var (c, cId) = await Account();
        using (a) using (b) using (c)
        {
            await Befriend(a, b, bId);
            await Befriend(a, c, cId);
            var first = await Send(a, bId, "hello 中文 😀");
            var second = await Send(b, aId, "reply");
            var third = await Send(a, bId, "same timestamp");
            await Send(a, cId, "separate conversation");
            Assert.Equal(aId, first.SenderId);
            Assert.Equal(bId, first.ReceiverId);
            Assert.Equal("hello 中文 😀", first.Content);
            Assert.Equal(DateTimeKind.Utc, first.CreatedAt.Kind);

            // Deliberately make timestamp order differ from insert/ID order.
            await using var connection = new MySqlConnection(factory.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE messages SET created_at = CASE WHEN id = @second THEN '2026-01-01 00:00:00' ELSE '2026-01-02 00:00:00' END WHERE id IN (@first, @second, @third)";
            command.Parameters.AddWithValue("@first", first.Id);
            command.Parameters.AddWithValue("@second", second.Id);
            command.Parameters.AddWithValue("@third", third.Id);
            Assert.Equal(3, await command.ExecuteNonQueryAsync());
            var expected = new[] { second.Id, first.Id, third.Id };
            var aHistory = (await a.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{bId}"))!;
            var bHistory = (await b.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{aId}"))!;
            Assert.Equal(expected, aHistory.Select(x => x.Id));
            Assert.Equal(expected, bHistory.Select(x => x.Id));
            Assert.Empty((await c.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{bId}"))!);
            Assert.Single((await c.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{aId}"))!);
        }
    }

    [Fact]
    public async Task Nonfriends_and_pending_requests_cannot_send()
    {
        var (a, _) = await Account();
        var (b, bId) = await Account();
        using (a) using (b)
        {
            using var denied = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = bId, content = "hello" });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var request = await a.PostAsJsonAsync("/api/friends/request", new { receiverId = bId });
            Assert.Equal(HttpStatusCode.Created, request.StatusCode);
            using var stillDenied = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = bId, content = "hello" });
            Assert.Equal(HttpStatusCode.Forbidden, stillDenied.StatusCode);
            Assert.Empty((await a.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{bId}"))!);
        }
    }

    [Fact]
    public async Task Unfriend_blocks_new_messages_but_retains_history()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        using (a) using (b)
        {
            await Befriend(a, b, bId);
            var saved = await Send(a, bId, "retained");
            using var removed = await a.DeleteAsync($"/api/friends/{bId}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            using var denied = await b.PostAsJsonAsync("/api/messages/send", new { receiverId = aId, content = "blocked" });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(saved.Id, Assert.Single((await b.GetFromJsonAsync<List<MessageResponse>>($"/api/messages/{aId}"))!).Id);
        }
    }

    [Fact]
    public async Task Invalid_content_and_targets_are_rejected()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        using (a) using (b)
        {
            await Befriend(a, b, bId);
            foreach (var content in new string?[] { null, "", " \t\n", new string('x', 2001) })
            {
                using var invalid = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = bId, content });
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            var boundary = await Send(a, bId, new string('x', 2000));
            Assert.Equal(2000, boundary.Content.Length);
            using var self = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = aId, content = "hello" });
            using var missing = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = long.MaxValue, content = "hello" });
            using var invalidId = await a.PostAsJsonAsync("/api/messages/send", new { receiverId = 0, content = "hello" });
            using var invalidGet = await a.GetAsync("/api/messages/0");
            using var selfGet = await a.GetAsync($"/api/messages/{aId}");
            using var missingGet = await a.GetAsync($"/api/messages/{long.MaxValue}");
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidGet.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, selfGet.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingGet.StatusCode);
        }
    }
}

