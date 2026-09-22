using System;
using System.Collections;
using System.Collections.Generic;
using SocialSystem.Client.Main;
using SocialSystem.Client.Network;
using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Posts
{
    public sealed class PostsView
    {
        private readonly SocialAppController app;
        private readonly PostApi api;
        private readonly RectTransform page;
        private readonly RectTransform list;
        private readonly Text pageLabel;
        private readonly Button previous;
        private readonly Button next;
        private readonly Dictionary<long, CommentDto> submitted = new Dictionary<long, CommentDto>();
        private readonly Dictionary<long, string> drafts = new Dictionary<long, string>();
        private int pageNumber = 1;
        private long pendingDelete;
        public InputField ContentInput { get; }
        public PostDto[] Items { get; private set; } = Array.Empty<PostDto>();
        public long LastPublishedId { get; private set; }

        public PostsView(SocialAppController app, Transform navigation)
        {
            this.app = app;
            api = new PostApi(app.Api);
            page = app.CreatePage("Posts");
            var compose = SocialUi.Row("ComposePost", page, 82);
            ContentInput = SocialUi.Input(compose, "分享动态（最多 2000 字符）", 2000, true);
            SocialUi.Button(compose, "发布动态", Publish);
            var paging = SocialUi.Row("Paging", page);
            previous = SocialUi.Button(paging, "上一页", () => Refresh(pageNumber - 1));
            pageLabel = SocialUi.Label(paging, "");
            next = SocialUi.Button(paging, "下一页", () => Refresh(pageNumber + 1));
            SocialUi.Button(paging, "刷新", () => Refresh(pageNumber));
            SocialUi.Label(page, "动态对所有用户可见。支持发表评论和查看评论数，暂无历史评论查询接口。", 16, 40);
            list = SocialUi.Scroll(page);
            SocialUi.Button(navigation, "动态", Open);
        }
        public void Open()
        {
            if (app.IsBusy) return;
            app.Show(page);
            Refresh(1);
        }
        public void Refresh(int number) { if (number >= 1 && number <= 1000000) app.Run(Load(number)); }
        private IEnumerator Load(int number)
        {
            ApiResponse<PostPage> response = null;
            yield return api.List(number, r => response = r);
            if (!app.RequireData(response)) yield break;
            if (response.Data.items == null) { app.SetStatus("动态列表响应无效。"); yield break; }
            if (number > 1 && response.Data.items.Length == 0)
            {
                yield return Load(number - 1);
                yield break;
            }
            pageNumber = number;
            Items = response.Data.items;
            pendingDelete = 0;
            SocialUi.Clear(list);
            pageLabel.text = "第 " + number + " 页 / 共 " + response.Data.total + " 条";
            previous.interactable = number > 1;
            next.interactable = (long)number * 10 < response.Data.total;
            foreach (var post in Items) Render(post);
            if (Items.Length == 0) SocialUi.Label(list, "暂无动态。");
            app.SetStatus("动态列表已更新。");
        }
        private void Render(PostDto post)
        {
            var card = SocialUi.Column("Post_" + post.id, list);
            card.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.19f, 0.26f);
            SocialUi.Body(card, (post.author?.nickname ?? "未知用户") + " · " + SocialUi.Time(post.createdAt) + " · #" + post.id);
            SocialUi.Body(card, post.content);
            var actions = SocialUi.Row("Actions", card);
            SocialUi.Label(actions, "赞 " + post.likeCount + " · 评论 " + post.commentCount);
            SocialUi.Button(actions, post.isLikedByMe ? "取消点赞" : "点赞", () => ToggleLike(post));
            if (post.author != null && post.author.id == app.Auth.CurrentUser.id)
            {
                Button delete = null;
                delete = SocialUi.Button(actions, "删除动态", () =>
                {
                    if (pendingDelete != post.id)
                    {
                        pendingDelete = post.id;
                        delete.GetComponentInChildren<Text>().text = "确认删除";
                        app.SetStatus("再次点击确认删除动态 #" + post.id + "。");
                    }
                    else Delete(post.id);
                });
            }
            var commentRow = SocialUi.Row("Comment", card, 70);
            var input = SocialUi.Input(commentRow, "评论（最多 500 字符）", 500, true);
            if (drafts.TryGetValue(post.id, out var draft)) input.text = draft;
            input.onValueChanged.AddListener(value => drafts[post.id] = value);
            SocialUi.Button(commentRow, "发表评论", () => Comment(post.id, input.text));
            if (submitted.TryGetValue(post.id, out var comment))
                SocialUi.Body(card, "本次会话最近提交的评论：" + comment.content);
        }
        private bool Validate(string value, int limit)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= limit) return true;
            app.SetStatus("内容不能为空，且最多 " + limit + " 字符。");
            return false;
        }
        public void Publish()
        {
            if (Validate(ContentInput.text, 2000)) app.Run(PublishFlow(ContentInput.text));
        }
        private IEnumerator PublishFlow(string content)
        {
            ApiResponse<PostDto> response = null;
            yield return api.Publish(content, r => response = r);
            if (!app.RequireData(response)) yield break;
            LastPublishedId = response.Data.id;
            ContentInput.text = "";
            yield return Load(1);
        }
        public void ToggleLike(PostDto post) { app.Run(LikeFlow(post.id, !post.isLikedByMe)); }
        private IEnumerator LikeFlow(long id, bool liked)
        {
            ApiResponse<string> response = null;
            yield return api.Like(id, liked, r => response = r);
            if (!app.Check(response)) yield break;
            yield return Load(pageNumber);
        }
        public void Comment(long id, string content)
        {
            if (Validate(content, 500)) app.Run(CommentFlow(id, content));
        }
        private IEnumerator CommentFlow(long id, string content)
        {
            ApiResponse<CommentDto> response = null;
            yield return api.Comment(id, content, r => response = r);
            if (!app.RequireData(response)) yield break;
            submitted[id] = response.Data;
            drafts.Remove(id);
            yield return Load(pageNumber);
        }
        public void Delete(long id) { app.Run(DeleteFlow(id)); }
        private IEnumerator DeleteFlow(long id)
        {
            ApiResponse<string> response = null;
            yield return api.Delete(id, r => response = r);
            if (!app.Check(response)) yield break;
            submitted.Remove(id);
            drafts.Remove(id);
            yield return Load(pageNumber);
        }
    }
}

