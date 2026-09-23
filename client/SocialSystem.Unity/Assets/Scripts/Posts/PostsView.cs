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
        private readonly ScrollRect feed;
        private readonly Text pageLabel;
        private readonly Button previous;
        private readonly Button next;
        private readonly Dictionary<long, string> drafts = new Dictionary<long, string>();
        private readonly Dictionary<long, CommentPage> comments = new Dictionary<long, CommentPage>();
        private readonly Dictionary<long, RectTransform> commentAreas = new Dictionary<long, RectTransform>();
        private readonly Dictionary<long, Button> commentButtons = new Dictionary<long, Button>();
        private readonly Dictionary<long, Text> counts = new Dictionary<long, Text>();
        private readonly HashSet<long> expanded = new HashSet<long>();
        private int pageNumber = 1;
        private long pendingDelete;
        private string commentError;
        public InputField ContentInput { get; }
        public PostDto[] Items { get; private set; } = Array.Empty<PostDto>();
        public long LastPublishedId { get; private set; }
        public bool CommentsExpanded(long id) => expanded.Contains(id);
        public CommentDto[] CommentItems(long id) => comments.TryGetValue(id, out var data) ? data.items : Array.Empty<CommentDto>();

        public PostsView(SocialAppController app, Transform navigation)
        {
            this.app = app;
            api = new PostApi(app.Api);
            page = app.CreatePage("Posts");
            page.GetComponent<VerticalLayoutGroup>().spacing = 8;
            var compose = Row("ComposePost", page, 52);
            ContentInput = SocialUi.Input(compose, "分享动态（最多 2000 字符）", 2000, true);
            Height(ContentInput.gameObject, 52);
            CompactButton(compose, "发布动态", Publish, 104, 36);
            var paging = Row("Paging", page, 32);
            previous = CompactButton(paging, "上一页", () => Refresh(pageNumber - 1), 80);
            pageLabel = SocialUi.Label(paging, "", 16, 32);
            pageLabel.alignment = TextAnchor.MiddleCenter;
            next = CompactButton(paging, "下一页", () => Refresh(pageNumber + 1), 80);
            CompactButton(paging, "刷新", () => Refresh(pageNumber), 76);
            list = SocialUi.Scroll(page);
            feed = list.parent.parent.GetComponent<ScrollRect>();
            feed.name = "PostFeed";
            list.GetComponent<VerticalLayoutGroup>().spacing = 12;
            SocialUi.Button(navigation, "动态", Open);
        }
        private static void Height(GameObject obj, float value)
        {
            var element = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = value;
            element.flexibleHeight = 0;
        }
        private static RectTransform Row(string name, Transform parent, float height)
        {
            var row = SocialUi.Row(name, parent, height);
            Height(row.gameObject, height);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row;
        }
        private static Button CompactButton(Transform parent, string title, UnityEngine.Events.UnityAction click, float width = 100, float height = 32)
        {
            var button = SocialUi.Button(parent, title, click);
            Height(button.gameObject, height);
            var element = button.GetComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.flexibleWidth = 0;
            button.GetComponentInChildren<Text>().fontSize = 16;
            return button;
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
            var scrollPosition = number == pageNumber ? feed.verticalNormalizedPosition : 1;
            ApiResponse<PostPage> response = null;
            yield return api.List(number, r => response = r);
            if (!app.RequireData(response)) yield break;
            if (response.Data.items == null) { app.SetStatus("动态列表响应无效。"); yield break; }
            if (number > 1 && response.Data.items.Length == 0)
            {
                yield return Load(Math.Max(1, (response.Data.total - 1) / 10 + 1));
                yield break;
            }
            pageNumber = number;
            Items = response.Data.items;
            pendingDelete = 0;
            SocialUi.Clear(list);
            commentAreas.Clear();
            commentButtons.Clear();
            counts.Clear();
            pageLabel.text = "第 " + number + " 页 · 共 " + response.Data.total + " 条";
            previous.interactable = number > 1;
            next.interactable = (long)number * 10 < response.Data.total;
            foreach (var post in Items) Render(post);
            if (Items.Length == 0) SocialUi.Label(list, "暂无动态。");
            commentError = null;
            foreach (var post in Items)
                if (expanded.Contains(post.id))
                    yield return LoadComments(post.id, comments.TryGetValue(post.id, out var cached) ? cached.page : 1);
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            feed.verticalNormalizedPosition = scrollPosition;
            app.SetStatus(commentError == null ? "动态列表已更新。" : "动态已更新；" + commentError);
        }
        private void Render(PostDto post)
        {
            var card = SocialUi.Column("Post_" + post.id, list);
            card.GetComponent<VerticalLayoutGroup>().spacing = 8;
            card.gameObject.AddComponent<Image>().color = SocialTheme.Surface;
            var author = SocialUi.Body(card, (post.author?.nickname ?? "未知用户") + " · " + SocialUi.Time(post.createdAt));
            author.fontSize = 16;
            author.color = SocialTheme.Muted;
            SocialUi.Body(card, post.content).name = "PostBody_" + post.id;
            counts[post.id] = SocialUi.Label(card, "", 16, 24);
            UpdateCounts(post);
            var actions = Row("Actions", card, 32);
            CompactButton(actions, post.isLikedByMe ? "取消点赞" : "点赞", () => ToggleLike(post), 88);
            var toggle = CompactButton(actions, expanded.Contains(post.id) ? "收起评论" : "查看评论", () => ToggleComments(post.id), 104);
            toggle.name = "ToggleComments_" + post.id;
            commentButtons[post.id] = toggle;
            if (post.author != null && post.author.id == app.Auth.CurrentUser.id)
            {
                Button delete = null;
                delete = CompactButton(actions, "删除动态", () =>
                {
                    if (pendingDelete != post.id)
                    {
                        pendingDelete = post.id;
                        delete.GetComponentInChildren<Text>().text = "确认删除";
                        app.SetStatus("再次点击确认删除这条动态。");
                    }
                    else Delete(post.id);
                }, 104);
            }
            var area = SocialUi.Column("Comments_" + post.id, card);
            area.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            area.GetComponent<VerticalLayoutGroup>().spacing = 6;
            commentAreas[post.id] = area;
            area.gameObject.SetActive(expanded.Contains(post.id));
            if (expanded.Contains(post.id)) SocialUi.Label(area, "正在加载评论…", 16, 30);
            var commentRow = Row("Comment", card, 44);
            var input = SocialUi.Input(commentRow, "评论（最多 500 字符）", 500, true);
            input.name = "CommentInput_" + post.id;
            Height(input.gameObject, 44);
            if (drafts.TryGetValue(post.id, out var draft)) input.text = draft;
            input.onValueChanged.AddListener(value => drafts[post.id] = value);
            var send = CompactButton(commentRow, "发表评论", () => Comment(post.id, input.text), 104);
            send.name = "SubmitComment_" + post.id;
        }
        private void UpdateCounts(PostDto post)
        {
            if (counts.TryGetValue(post.id, out var label))
                label.text = "赞 " + post.likeCount + " · 评论 " + post.commentCount;
        }
        public void ToggleComments(long id)
        {
            if (app.IsBusy || !commentAreas.TryGetValue(id, out var area)) return;
            if (expanded.Remove(id))
            {
                area.gameObject.SetActive(false);
                commentButtons[id].GetComponentInChildren<Text>().text = "查看评论";
                return;
            }
            expanded.Add(id);
            area.gameObject.SetActive(true);
            commentButtons[id].GetComponentInChildren<Text>().text = "收起评论";
            app.Run(ShowComments(id, 1));
        }
        private IEnumerator ShowComments(long id, int number)
        {
            commentError = null;
            yield return LoadComments(id, number);
            app.SetStatus(commentError ?? "评论已更新。");
        }
        private IEnumerator LoadComments(long id, int number)
        {
            if (!commentAreas.TryGetValue(id, out var area)) yield break;
            SocialUi.Clear(area);
            SocialUi.Label(area, "正在加载评论…", 16, 30);
            ApiResponse<CommentPage> response = null;
            yield return api.Comments(id, number, r => response = r);
            if (response == null || !response.Success || response.Data?.items == null)
            {
                commentError = response?.StatusCode == 404 ? "动态已不存在，请刷新动态列表。" :
                    "评论加载失败，请重试。";
                SocialUi.Clear(area);
                SocialUi.Body(area, commentError);
                CompactButton(area, "重试", () => app.Run(ShowComments(id, number)), 88);
                yield break;
            }
            var data = response.Data;
            if (number > 1 && data.items.Length == 0)
            {
                yield return LoadComments(id, Math.Max(1, (data.total - 1) / 10 + 1));
                yield break;
            }
            comments[id] = data;
            var post = Array.Find(Items, x => x.id == id);
            if (post != null) { post.commentCount = data.total; UpdateCounts(post); }
            RenderComments(id, area, data);
        }
        private void RenderComments(long id, RectTransform area, CommentPage data)
        {
            SocialUi.Clear(area);
            var paging = Row("CommentPaging", area, 30);
            SocialUi.Label(paging, "最新评论 · 第 " + data.page + " 页 · 共 " + data.total + " 条", 16, 30);
            var prev = CompactButton(paging, "上一页", () => app.Run(ShowComments(id, data.page - 1)), 76, 30);
            prev.name = "CommentPrevious_" + id;
            prev.interactable = data.page > 1;
            var nextPage = CompactButton(paging, "下一页", () => app.Run(ShowComments(id, data.page + 1)), 76, 30);
            nextPage.name = "CommentNext_" + id;
            nextPage.interactable = (long)data.page * 10 < data.total;
            CompactButton(paging, "刷新评论", () => app.Run(ShowComments(id, data.page)), 100, 30);
            var scrollRoot = SocialUi.Rect("CommentScroll_" + id, area);
            Height(scrollRoot.gameObject, 150);
            var scroll = scrollRoot.gameObject.AddComponent<PostCommentsScrollRect>();
            scroll.Outer = feed;
            var viewport = SocialUi.Rect("Viewport", scrollRoot);
            SocialUi.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = SocialTheme.Background;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = SocialUi.Column("CommentContent", viewport);
            content.GetComponent<VerticalLayoutGroup>().spacing = 8;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            foreach (var comment in data.items)
            {
                var row = SocialUi.Column("Comment_" + comment.id, content);
                var layout = row.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(4, 4, 4, 4);
                layout.spacing = 4;
                var identity = comment.author;
                var name = string.IsNullOrWhiteSpace(identity?.nickname) ? identity?.username ?? "未知用户" : identity.nickname;
                var header = SocialUi.Body(row, name + (string.IsNullOrEmpty(identity?.username) ? "" : "（" + identity.username + "）"));
                header.fontSize = 16;
                var body = SocialUi.Body(row, comment.content);
                body.fontSize = 16;
                body.name = "CommentBody_" + comment.id;
                var time = SocialUi.Label(row, SocialUi.Time(comment.createdAt), 14, 22);
                time.color = SocialTheme.Muted;
            }
            if (data.items.Length == 0) SocialUi.Label(content, "暂无评论", 16, 30);
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
            feed.verticalNormalizedPosition = 1;
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
            drafts.Remove(id);
            expanded.Add(id);
            comments.Remove(id); // Newly submitted comments are on the first, newest-first page.
            yield return Load(pageNumber);
        }
        public void Delete(long id) { app.Run(DeleteFlow(id)); }
        private IEnumerator DeleteFlow(long id)
        {
            ApiResponse<string> response = null;
            yield return api.Delete(id, r => response = r);
            if (!app.Check(response)) yield break;
            expanded.Remove(id);
            comments.Remove(id);
            drafts.Remove(id);
            yield return Load(pageNumber);
        }
    }
}
