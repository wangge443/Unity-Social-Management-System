using System;
using System.Collections;
using SocialSystem.Client.Network;

namespace SocialSystem.Client.Posts
{
    [Serializable] public sealed class PostAuthor { public long id; public string username; public string nickname; public string avatarKey; }
    [Serializable] public sealed class PostDto
    {
        public long id;
        public PostAuthor author;
        public string content;
        public string createdAt;
        public int commentCount;
        public int likeCount;
        public bool isLikedByMe;
    }
    [Serializable] public sealed class PostPage { public PostDto[] items; public int page; public int pageSize; public int total; }
    [Serializable] public sealed class ContentBody { public string content; }
    [Serializable] public sealed class CommentDto { public long id; public long postId; public PostAuthor author; public string content; public string createdAt; }
    [Serializable] public sealed class CommentPage { public CommentDto[] items; public int page; public int pageSize; public int total; }
    public sealed class PostApi
    {
        private readonly ApiClient api;
        public PostApi(ApiClient api) { this.api = api; }
        public IEnumerator List(int page, Action<ApiResponse<PostPage>> completed) =>
            api.Get<PostPage>("/api/posts?page=" + page + "&pageSize=10", completed);
        public IEnumerator Publish(string content, Action<ApiResponse<PostDto>> completed) =>
            api.Post<PostDto>("/api/posts", new ContentBody { content = content }, completed);
        public IEnumerator Comment(long id, string content, Action<ApiResponse<CommentDto>> completed) =>
            api.Post<CommentDto>("/api/posts/" + id + "/comments", new ContentBody { content = content }, completed);
        public IEnumerator Comments(long id, int page, Action<ApiResponse<CommentPage>> completed) =>
            api.Get<CommentPage>("/api/posts/" + id + "/comments?page=" + page + "&pageSize=10", completed);
        public IEnumerator Like(long id, bool liked, Action<ApiResponse<string>> completed) =>
            liked ? api.Post<string>("/api/posts/" + id + "/like", null, completed)
                  : api.Delete<string>("/api/posts/" + id + "/like", completed);
        public IEnumerator Delete(long id, Action<ApiResponse<string>> completed) =>
            api.Delete<string>("/api/posts/" + id, completed);
    }
}

