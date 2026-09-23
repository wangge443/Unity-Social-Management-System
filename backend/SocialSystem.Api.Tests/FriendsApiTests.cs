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
    private async Task<(HttpClient Client, long Id)> Account(string? nickname = null)
    {
        var client = factory.CreateClient();
        var username = "friend_" + Guid.NewGuid().ToString("N")[..20];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password, nickname = nickname ?? "Friend test" });
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


    [Fact]
    public async Task Search_matches_names_excludes_self_and_pages_safe_results()
    {
        var key = "搜索_" + Guid.NewGuid().ToString("N")[..12];
        var (a, aId) = await Account(key);
        var (b, bId) = await Account(key + "乙");
        var (c, cId) = await Account(key + "丙");
        using (a) using (b) using (c)
        {
            var url = "/api/friends/search-users?keyword=" + Uri.EscapeDataString(" " + key + " ");
            var first = (await a.GetFromJsonAsync<UserSearchListResponse>(url + "&pageSize=1"))!;
            var second = (await a.GetFromJsonAsync<UserSearchListResponse>(url + "&pageSize=1&page=2"))!;
            Assert.Equal(2, first.Total);
            Assert.Equal(2, second.Total);
            Assert.Equal(new[] { bId, cId }.Order(), first.Items.Concat(second.Items).Select(x => x.UserId).Order());
            Assert.DoesNotContain(first.Items.Concat(second.Items), x => x.UserId == aId);
            Assert.All(first.Items.Concat(second.Items), x => Assert.Equal("none", x.Relationship));
            var target = Assert.Single(first.Items);
            var byUsername = (await a.GetFromJsonAsync<UserSearchListResponse>(
                "/api/friends/search-users?keyword=" + target.Username.ToUpperInvariant()))!;
            Assert.Equal(target.UserId, Assert.Single(byUsername.Items).UserId);
            Assert.Empty((await a.GetFromJsonAsync<UserSearchListResponse>(url + "&page=3&pageSize=1"))!.Items);
            Assert.Empty((await a.GetFromJsonAsync<UserSearchListResponse>(
                "/api/friends/search-users?keyword=" + Guid.NewGuid().ToString("N")))!.Items);
            using var raw = await a.GetAsync(url);
            var json = await raw.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bio", json, StringComparison.OrdinalIgnoreCase);
            // A literal percent is not an SQL LIKE wildcard.
            Assert.Empty((await a.GetFromJsonAsync<UserSearchListResponse>(
                "/api/friends/search-users?keyword=" + Uri.EscapeDataString(key + "%")))!.Items);
        }
    }

    [Fact]
    public async Task Search_tracks_outgoing_incoming_accept_reject_and_removal()
    {
        var key = "关系_" + Guid.NewGuid().ToString("N")[..12];
        var (a, aId) = await Account(key);
        var (b, bId) = await Account(key);
        using (a) using (b)
        {
            var url = "/api/friends/search-users?keyword=" + Uri.EscapeDataString(key);
            async Task<string> State(HttpClient client) =>
                Assert.Single((await client.GetFromJsonAsync<UserSearchListResponse>(url))!.Items).Relationship;
            Assert.Equal("none", await State(a));
            var request = await Send(a, bId);
            Assert.Equal("outgoing", await State(a));
            Assert.Equal("incoming", await State(b));
            using var duplicate = await a.PostAsJsonAsync("/api/friends/request", new { receiverId = bId });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var reverse = await b.PostAsJsonAsync("/api/friends/request", new { receiverId = aId });
            Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
            using var reject = await b.PostAsync($"/api/friends/reject/{request}", null);
            Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
            Assert.Equal("none", await State(a));
            request = await Send(a, bId);
            using var accept = await b.PostAsync($"/api/friends/accept/{request}", null);
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            Assert.Equal("friends", await State(a));
            Assert.Equal("friends", await State(b));
            using var delete = await a.DeleteAsync($"/api/friends/{bId}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            Assert.Equal("none", await State(a));
        }
    }

    [Fact]
    public async Task Search_rejects_invalid_keyword_and_pagination()
    {
        var (client, _) = await Account();
        using (client)
        {
            foreach (var query in new[] { "", "?keyword=", "?keyword=%20%20", "?keyword=" + new string('x', 33),
                "?keyword=test&page=0", "?keyword=test&pageSize=0", "?keyword=test&pageSize=101", "?keyword=test&page=1000001" })
            {
                using var response = await client.GetAsync("/api/friends/search-users" + query);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
    }

    [Theory]
    [InlineData("POST", "/api/friends/request")]
    [InlineData("POST", "/api/friends/accept/1")]
    [InlineData("DELETE", "/api/friends/1")]
    [InlineData("GET", "/api/friends/search-users?keyword=test")]
    [InlineData("GET", "/api/friends")]
    [InlineData("GET", "/api/friends/requests")]
    [InlineData("POST", "/api/friends/reject/1")]
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

    [Fact]
    public async Task Incoming_is_private_paginated_and_pending_only()
    {
        var (a, aId) = await Account(); var (b, bId) = await Account();
        var (c, _) = await Account(); var (d, _) = await Account();
        using (a) using (b) using (c) using (d)
        {
            var first = await Send(a, bId); var second = await Send(c, bId); var third = await Send(d, bId);
            var page = (await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests?pageSize=2"))!;
            Assert.Equal(3, page.Total);
            Assert.Equal(new[] { third, second }, page.Items.Select(x => x.RequestId));
            var tail = (await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests?page=2&pageSize=2"))!;
            var entry = Assert.Single(tail.Items);
            Assert.Equal(first, entry.RequestId); Assert.Equal(aId, entry.SenderId);
            var identity = (await a.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
            Assert.Equal(identity.Username, entry.Username); Assert.Equal(identity.Nickname, entry.Nickname);
            Assert.Empty((await a.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests"))!.Items);
            Assert.Empty((await c.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests?receiverId=" + bId))!.Items);
            using var raw = await b.GetAsync("/api/friends/requests");
            Assert.DoesNotContain("password", await raw.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            using var accepted = await b.PostAsync("/api/friends/accept/" + first, null);
            using var rejected = await b.PostAsync("/api/friends/reject/" + second, null);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode); Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Equal(third, Assert.Single((await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests"))!.Items).RequestId);
        }
    }

    [Fact]
    public async Task Reject_checks_ownership_keeps_history_and_allows_new_request()
    {
        var (a, _) = await Account(); var (b, bId) = await Account(); var (c, _) = await Account();
        using (a) using (b) using (c)
        {
            var id = await Send(a, bId);
            using var sender = await a.PostAsync("/api/friends/reject/" + id, null);
            using var stranger = await c.PostAsync("/api/friends/reject/" + id, null);
            Assert.Equal(HttpStatusCode.Forbidden, sender.StatusCode); Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
            Assert.Single((await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests"))!.Items);
            using var reject = await b.PostAsync("/api/friends/reject/" + id, null);
            Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
            Assert.Equal("rejected", (await reject.Content.ReadFromJsonAsync<FriendRequestResponse>())!.Status);
            Assert.Empty((await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests"))!.Items);
            Assert.Empty((await a.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
            Assert.Empty((await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
            using var again = await b.PostAsync("/api/friends/reject/" + id, null);
            using var accept = await b.PostAsync("/api/friends/accept/" + id, null);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
            await using var connection = new MySqlConnector.MySqlConnection(factory.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM friend_requests WHERE id=@id AND status=2 AND handled_at >= created_at";
            command.Parameters.AddWithValue("@id", id);
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            Assert.NotEqual(id, await Send(a, bId));
        }
    }

    [Fact]
    public async Task Concurrent_accept_and_reject_have_one_winner()
    {
        var (a, _) = await Account(); var (b, bId) = await Account();
        using (a) using (b)
        {
            var id = await Send(a, bId);
            var responses = await Task.WhenAll(b.PostAsync("/api/friends/accept/" + id, null), b.PostAsync("/api/friends/reject/" + id, null));
            try
            {
                var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
                var state = (await winner.Content.ReadFromJsonAsync<FriendRequestResponse>())!.Status;
                var expected = state == "accepted" ? 1 : 0;
                Assert.Equal(expected, (await a.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Total);
                Assert.Equal(expected, (await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Total);
                Assert.Empty((await b.GetFromJsonAsync<IncomingFriendRequestListResponse>("/api/friends/requests"))!.Items);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
    }

    [Fact]
    public async Task Concurrent_rejects_and_late_reject_cannot_process_twice()
    {
        var (a, _) = await Account(); var (b, bId) = await Account();
        using (a) using (b)
        {
            var id = await Send(a, bId);
            var responses = await Task.WhenAll(b.PostAsync("/api/friends/reject/" + id, null), b.PostAsync("/api/friends/reject/" + id, null));
            try
            {
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            }
            finally { foreach (var response in responses) response.Dispose(); }
            var next = await Send(a, bId);
            using var accepted = await b.PostAsync("/api/friends/accept/" + next, null);
            using var late = await b.PostAsync("/api/friends/reject/" + next, null);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode); Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
            Assert.Single((await b.GetFromJsonAsync<FriendListResponse>("/api/friends"))!.Items);
        }
    }

    [Fact]
    public async Task New_endpoints_validate_page_and_request_id()
    {
        var (client, _) = await Account();
        using (client)
        {
            foreach (var query in new[] { "?page=0", "?page=1000001", "?pageSize=0", "?pageSize=101" })
            {
                using var response = await client.GetAsync("/api/friends/requests" + query);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            using var zero = await client.PostAsync("/api/friends/reject/0", null);
            using var missing = await client.PostAsync("/api/friends/reject/" + long.MaxValue, null);
            Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }
}

