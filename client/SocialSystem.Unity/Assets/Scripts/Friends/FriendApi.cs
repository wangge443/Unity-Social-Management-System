using System;
using System.Collections;
using SocialSystem.Client.Network;

namespace SocialSystem.Client.Friends
{
    [Serializable] public sealed class FriendDto
    {
        public long friendId;
        public string username;
        public string nickname;
        public string avatarKey;
        public string createdAt;
    }
    [Serializable] public sealed class FriendPage { public FriendDto[] items; public int page; public int pageSize; public int total; }
    [Serializable] public sealed class FriendRequestDto { public long requestId; public long senderId; public long receiverId; public string status; }
    [Serializable] public sealed class FriendRequestBody { public long receiverId; }
    [Serializable] public sealed class IncomingFriendRequestDto { public long requestId; public long senderId; public string username; public string nickname; public string createdAt; }
    [Serializable] public sealed class IncomingFriendRequestPage { public IncomingFriendRequestDto[] items; public int page; public int pageSize; public int total; }
    [Serializable] public sealed class UserSearchDto { public long userId; public string username; public string nickname; public string relationship; }
    [Serializable] public sealed class UserSearchPage { public UserSearchDto[] items; public int page; public int pageSize; public int total; }
    public sealed class FriendApi
    {
        private readonly ApiClient api;
        public FriendApi(ApiClient api) { this.api = api; }
        public IEnumerator List(int page, Action<ApiResponse<FriendPage>> completed) =>
            api.Get<FriendPage>("/api/friends?page=" + page + "&pageSize=10", completed);
        public IEnumerator Search(string keyword, int page, Action<ApiResponse<UserSearchPage>> completed) =>
            api.Get<UserSearchPage>("/api/friends/search-users?keyword=" + Uri.EscapeDataString(keyword) + "&page=" + page + "&pageSize=10", completed);
        public IEnumerator Request(long id, Action<ApiResponse<FriendRequestDto>> completed) =>
            api.Post<FriendRequestDto>("/api/friends/request", new FriendRequestBody { receiverId = id }, completed);
        public IEnumerator Accept(long id, Action<ApiResponse<FriendRequestDto>> completed) =>
            api.Post<FriendRequestDto>("/api/friends/accept/" + id, null, completed);
        public IEnumerator Incoming(int page, Action<ApiResponse<IncomingFriendRequestPage>> completed) =>
            api.Get<IncomingFriendRequestPage>("/api/friends/requests?page=" + page + "&pageSize=10", completed);
        public IEnumerator Reject(long id, Action<ApiResponse<FriendRequestDto>> completed) =>
            api.Post<FriendRequestDto>("/api/friends/reject/" + id, null, completed);
        public IEnumerator Delete(long id, Action<ApiResponse<string>> completed) =>
            api.Delete<string>("/api/friends/" + id, completed);
    }
}

