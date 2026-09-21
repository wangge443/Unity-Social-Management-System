using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.DTOs.Friends;
using Xunit;

namespace SocialSystem.Api.Tests;

public sealed class FriendsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Client, long Id)> Account()
    {
        var client = factory.CreateClient();
        var username = "friend_" + Guid.NewGuid().ToString("N")[..20];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password, nickname = "Friend test" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = (await registration.Content.ReadFromJsonAsync<UserResponse>())!;
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, user.Id);
    }

    private static async Task<long> Send(HttpClient client, long receiverId)
    {
        using var response = await client.PostAsJsonAsync("/api/friends/request", new { receiverId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FriendRequestResponse>())!.RequestId;
    }

    [Theory]
    [InlineData("POST", "/api/friends/request")]
    [InlineData("POST", "/api/friends/accept/1")]
    [InlineData("DELETE", "/api/friends/1")]
    [InlineData("GET", "/api/friends")]
    public async Task All_endpoints_require_authentication(string method, string path)
    {
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(new HttpMethod(method), path);
        if (path.EndsWith("request")) message.Content = JsonContent.Create(new { receiverId = 1 });
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_enforces_receiver_ownership_and_symmetric_lists()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        var (c, _) = await Account();
        using (a) using (b) using (c)
        {
            var requestId = await Send(a, bId);
            using var senderAccept = await a.PostAsync($"/api/friends/accept/{requestId}", null);
            using var strangerAccept = await c.PostAsync($"/api/friends/accept/{requestId}", null);
            Assert.Equal(HttpStatusCode.Forbidden, senderAccept.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, strangerAccept.StatusCode);
            using var accepted = await b.PostAsync($"/api/friends/accept/{requestId}", null);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var repeated = await b.PostAsync($"/api/friends/accept/{requestId}", null);
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
            using var alreadyFriends = await a.PostAsJsonAsync("/api/friends/request", new { receiverId = bId });
            Assert.Equal(HttpStatusCode.Conflict, alreadyFriends.StatusCode);

            using var strangerDelete = await c.DeleteAsync($"/api/friends/{bId}");
            Assert.Equal(HttpStatusCode.NoContent, strangerDelete.StatusCode);
            var aList = (await a.GetFromJsonAsync<FriendListResponse>("/api/friends"))!;
            var bList = (await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!;
            Assert.Equal(bId, Assert.Single(aList.Items).FriendId);
            Assert.Equal(aId, Assert.Single(bList.Items).FriendId);
            Assert.Equal(1, aList.Total);
            Assert.Empty((await c.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
            using var rawList = await a.GetAsync("/api/friends");
            Assert.DoesNotContain("password", await rawList.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.Empty((await a.GetFromJsonAsync<FriendListResponse>("/api/friends?page=2&pageSize=1"))!.Items);

            using var removed = await a.DeleteAsync($"/api/friends/{bId}");
            using var removedAgain = await a.DeleteAsync($"/api/friends/{bId}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, removedAgain.StatusCode);
            Assert.Empty((await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
            using var staleAccept = await b.PostAsync($"/api/friends/accept/{requestId}", null);
            Assert.Equal(HttpStatusCode.Conflict, staleAccept.StatusCode);
            var newRequestId = await Send(b, aId);
            Assert.NotEqual(requestId, newRequestId);
            using var acceptedAgain = await a.PostAsync($"/api/friends/accept/{newRequestId}", null);
            Assert.Equal(HttpStatusCode.OK, acceptedAgain.StatusCode);
        }
    }

    [Fact]
    public async Task Invalid_targets_and_pagination_are_rejected()
    {
        var (client, id) = await Account();
        using (client)
        {
            using var self = await client.PostAsJsonAsync("/api/friends/request", new { receiverId = id });
            using var missing = await client.PostAsJsonAsync("/api/friends/request", new { receiverId = long.MaxValue });
            using var invalid = await client.PostAsJsonAsync("/api/friends/request", new { receiverId = 0 });
            using var empty = await client.PostAsJsonAsync("/api/friends/request", new { });
            using var invalidPage = await client.GetAsync("/api/friends?page=0&pageSize=101");
            using var missingRequest = await client.PostAsync($"/api/friends/accept/{long.MaxValue}", null);
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingRequest.StatusCode);
        }
    }

    [Fact]
    public async Task Simultaneous_reverse_requests_and_accepts_create_one_relationship()
    {
        var (a, aId) = await Account();
        var (b, bId) = await Account();
        using (a) using (b)
        {
            var requests = await Task.WhenAll(
                a.PostAsJsonAsync("/api/friends/request", new { receiverId = bId }),
                b.PostAsJsonAsync("/api/friends/request", new { receiverId = aId }));
            FriendRequestResponse request;
            try
            {
                var winner = Assert.Single(requests, x => x.StatusCode == HttpStatusCode.Created);
                Assert.Single(requests, x => x.StatusCode == HttpStatusCode.Conflict);
                request = (await winner.Content.ReadFromJsonAsync<FriendRequestResponse>())!;
            }
            finally { foreach (var response in requests) response.Dispose(); }
            var recipient = request.ReceiverId == aId ? a : b;
            var accepts = await Task.WhenAll(
                recipient.PostAsync($"/api/friends/accept/{request.RequestId}", null),
                recipient.PostAsync($"/api/friends/accept/{request.RequestId}", null));
            try
            {
                Assert.Single(accepts, x => x.StatusCode == HttpStatusCode.OK);
                Assert.Single(accepts, x => x.StatusCode == HttpStatusCode.Conflict);
            }
            finally { foreach (var response in accepts) response.Dispose(); }
            Assert.Single((await a.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
            Assert.Single((await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
        }
    }
}

