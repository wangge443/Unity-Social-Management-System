using System;
using System.Collections;
using SocialSystem.Client.Friends;
using SocialSystem.Client.Main;
using SocialSystem.Client.Network;
using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Messages
{
    public sealed class ChatView
    {
        private readonly SocialAppController app;
        private readonly MessageApi api;
        private readonly ConversationStore contacts;
        private readonly RectTransform page;
        private readonly RectTransform history;
        private readonly RectTransform recent;
        private readonly Text title;
        private readonly Text hint;
        private readonly Button refreshButton;
        private readonly Button sendButton;
        private ConversationContact selected;
        public InputField ContentInput { get; }
        public long PeerId => selected?.id ?? 0;
        public string PeerUsername => selected?.username ?? "";
        public string PeerNickname => selected?.nickname ?? "";
        public int RecentCount => contacts.Items.Count;
        public MessageDto[] Items { get; private set; } = Array.Empty<MessageDto>();

        public ChatView(SocialAppController app, Transform navigation)
        {
            this.app = app;
            api = new MessageApi(app.Api);
            contacts = new ConversationStore(app.Api.baseUrl, app.Auth.CurrentUser.id);
            page = app.CreatePage("Chat");
            var columns = SocialUi.Row("ChatColumns", page);
            columns.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var space = columns.GetComponent<LayoutElement>();
            space.minHeight = 0;
            space.preferredHeight = -1;
            space.flexibleHeight = 1;
            var sidebar = SocialUi.Column("ConversationList", columns);
            var sidebarSize = sidebar.gameObject.AddComponent<LayoutElement>();
            sidebarSize.minWidth = sidebarSize.preferredWidth = 190;
            sidebarSize.flexibleWidth = 0;
            sidebar.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            SocialUi.Label(sidebar, "最近会话", 20, 32);
            SocialUi.Button(sidebar, "选择好友", () => app.Friends.Open());
            recent = SocialUi.Scroll(sidebar);

            var conversation = SocialUi.Column("Conversation", columns);
            var conversationSize = conversation.gameObject.AddComponent<LayoutElement>();
            conversationSize.minWidth = conversationSize.preferredWidth = 0;
            conversationSize.flexibleWidth = 1;
            conversation.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            title = SocialUi.Body(conversation, "请选择一位好友");
            title.fontSize = 20;
            var toolbar = Row("Toolbar", conversation, 42);
            hint = SocialUi.Label(toolbar, "点击左侧“选择好友”开始聊天。", 14, 34);
            refreshButton = SocialUi.Button(toolbar, "刷新消息", Refresh);
            var refreshSize = refreshButton.GetComponent<LayoutElement>();
            refreshSize.minWidth = refreshSize.preferredWidth = 108;
            refreshSize.flexibleWidth = 0;
            history = SocialUi.Scroll(conversation);
            var compose = Row("Compose", conversation, 82);
            ContentInput = SocialUi.Input(compose, "输入消息（最多 2000 字符）", 2000, true);
            sendButton = SocialUi.Button(compose, "发送", Send);
            var sendSize = sendButton.GetComponent<LayoutElement>();
            sendSize.minWidth = sendSize.preferredWidth = 80;
            sendSize.flexibleWidth = 0;
            SocialUi.Button(navigation, "消息", OpenPage);
            RenderRecent();
            UpdateSelection();
            SocialUi.Body(history, "从好友列表点击昵称或“聊天”，即可打开双方历史。");
        }
        private static RectTransform Row(string name, Transform parent, float height)
        {
            var row = SocialUi.Row(name, parent, height);
            row.GetComponent<LayoutElement>().flexibleHeight = 0;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row;
        }
        public void OpenPage()
        {
            if (app.IsBusy) return;
            app.Show(page);
            RenderRecent();
            if (selected != null) app.Run(Load(false));
        }
        public void OpenFriend(FriendDto friend)
        {
            if (app.IsBusy || friend == null || friend.friendId <= 0 || friend.friendId == app.Auth.CurrentUser.id) return;
            Select(contacts.Remember(friend.friendId, friend.username, friend.nickname, false));
        }
        public void OpenRecent(long id)
        {
            if (app.IsBusy) return;
            var contact = contacts.Find(id);
            if (contact != null) Select(contact);
        }
        private void Select(ConversationContact contact)
        {
            var changed = PeerId != contact.id;
            if (changed)
            {
                ContentInput.text = "";
                Items = Array.Empty<MessageDto>();
                SocialUi.Clear(history);
            }
            selected = contact;
            app.Show(page);
            RenderRecent();
            UpdateSelection();
            app.Run(Load(changed));
        }
        public void MarkRemoved(FriendDto friend)
        {
            var contact = contacts.Remember(friend.friendId, friend.username, friend.nickname, true);
            if (PeerId == contact.id) { selected = contact; UpdateSelection(); }
            RenderRecent();
        }
        private void RenderRecent()
        {
            SocialUi.Clear(recent);
            foreach (var contact in contacts.Items)
            {
                var button = SocialUi.Button(recent, contact.nickname + "\n@" + contact.username, () => OpenRecent(contact.id));
                button.name = "RecentContact_" + contact.id;
                var size = button.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 100;
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 14;
                label.alignment = TextAnchor.MiddleLeft;
                button.GetComponent<Image>().color = contact.id == PeerId ? SocialTheme.Accent : SocialTheme.Surface;
            }
            if (contacts.Items.Count == 0) SocialUi.Body(recent, "暂无会话\n先从好友列表选择一位好友。");
        }
        private void UpdateSelection()
        {
            title.text = selected == null ? "请选择一位好友" : selected.nickname + "\n用户名：" + selected.username;
            hint.text = selected == null ? "从好友列表选择聊天对象。" :
                selected.removed ? "已移除好友，可继续查看历史。" : "消息按时间排列，点击刷新获取新消息。";
            refreshButton.interactable = selected != null;
            sendButton.interactable = ContentInput.interactable = selected != null && !selected.removed;
        }
        public void Refresh()
        {
            if (!app.IsBusy && selected != null) app.Run(Load(false));
        }
        private IEnumerator Load(bool toBottom)
        {
            var scroll = history.GetComponentInParent<ScrollRect>();
            var oldPosition = scroll.verticalNormalizedPosition;
            var followLatest = toBottom || oldPosition <= 0.05f;
            ApiResponse<MessageDto[]> response = null;
            yield return api.History(PeerId, r => response = r);
            if (!app.RequireData(response)) yield break;
            Items = response.Data;
            SocialUi.Clear(history);
            foreach (var message in Items) RenderMessage(message);
            if (Items.Length == 0) SocialUi.Body(history, "暂无消息，向好友打个招呼吧。");
            // Let text wrapping and ContentSizeFitter settle before positioning the scroll.
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(history);
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = followLatest ? 0 : oldPosition;
            app.SetStatus("消息历史已更新，共 " + Items.Length + " 条。");
        }
        private void RenderMessage(MessageDto message)
        {
            var mine = message.senderId == app.Auth.CurrentUser.id;
            var row = SocialUi.Rect("Message_" + message.id, history);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            if (mine) Spacer(row);
            var bubble = SocialUi.Column("Bubble", row);
            var size = bubble.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 0;
            size.flexibleWidth = 3;
            var vertical = bubble.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(12, 12, 8, 8);
            vertical.spacing = 4;
            bubble.gameObject.AddComponent<Image>().color = mine ? new Color(0.13f, 0.28f, 0.46f) : SocialTheme.Surface;
            var sender = SocialUi.Body(bubble, mine ? "我" : PeerNickname);
            sender.fontSize = 14;
            sender.color = SocialTheme.Muted;
            SocialUi.Body(bubble, message.content);
            var time = SocialUi.Label(bubble, SocialUi.Time(message.createdAt), 12, 20);
            time.color = SocialTheme.Muted;
            time.alignment = TextAnchor.MiddleRight;
            if (!mine) Spacer(row);
        }
        private static void Spacer(Transform parent)
        {
            var spacer = SocialUi.Rect("Space", parent).gameObject.AddComponent<LayoutElement>();
            spacer.minWidth = spacer.preferredWidth = 0;
            spacer.flexibleWidth = 1;
        }
        public void Send()
        {
            if (app.IsBusy) return;
            if (selected == null) { app.SetStatus("请先从好友列表选择聊天对象。"); return; }
            if (selected.removed) { app.SetStatus("仅好友可发送消息；此会话可以继续查看历史。"); return; }
            if (string.IsNullOrWhiteSpace(ContentInput.text) || ContentInput.text.Length > 2000)
            {
                app.SetStatus("消息不能为空，且最多 2000 字符。");
                return;
            }
            app.Run(SendFlow(ContentInput.text));
        }
        private IEnumerator SendFlow(string content)
        {
            ApiResponse<MessageDto> response = null;
            yield return api.Send(PeerId, content, r => response = r);
            if (!app.RequireData(response))
            {
                if (response?.StatusCode == 403)
                {
                    selected = contacts.Remember(PeerId, PeerUsername, PeerNickname, true);
                    UpdateSelection();
                }
                yield break;
            }
            ContentInput.text = "";
            yield return Load(true);
        }
    }
}
