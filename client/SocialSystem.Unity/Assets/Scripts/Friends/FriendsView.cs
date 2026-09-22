using System;
using System.Collections;
using System.Collections.Generic;
using SocialSystem.Client.Main;
using SocialSystem.Client.Network;
using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Friends
{
    public sealed class FriendsView
    {
        private readonly SocialAppController app;
        private readonly FriendApi api;
        private readonly RectTransform page;
        private readonly RectTransform requestPage;
        private readonly RectTransform list;
        private readonly Text pageLabel;
        private readonly Button previous;
        private readonly Button next;
        private readonly Action<FriendDto> openChat;
        private readonly Dictionary<long, Button> deleteButtons = new Dictionary<long, Button>();
        private int pageNumber = 1;
        private long pendingDelete;
        public InputField ReceiverInput { get; }
        public InputField RequestInput { get; }
        public FriendDto[] Items { get; private set; } = Array.Empty<FriendDto>();
        public long LastRequestId { get; private set; }

        public FriendsView(SocialAppController app, Transform navigation, Action<FriendDto> openChat)
        {
            this.app = app;
            this.openChat = openChat;
            api = new FriendApi(app.Api);
            page = app.CreatePage("Friends");
            var header = Row("Header", page);
            SocialUi.Label(header, "我的好友", 24);
            FixedWidth(SocialUi.Button(header, "好友申请", OpenRequests), 130);
            var paging = Row("Paging", page, 42);
            pageLabel = SocialUi.Label(paging, "", 16, 38);
            previous = SocialUi.Button(paging, "上一页", () => Refresh(pageNumber - 1));
            next = SocialUi.Button(paging, "下一页", () => Refresh(pageNumber + 1));
            FixedWidth(previous, 88);
            FixedWidth(next, 88);
            FixedWidth(SocialUi.Button(paging, "刷新", () => Refresh(pageNumber)), 88);
            list = SocialUi.Scroll(page);

            requestPage = app.CreatePage("FriendRequests");
            SocialUi.Label(requestPage, "好友申请", 24, 42);
            var send = Row("SendRequest", requestPage);
            ReceiverInput = SocialUi.Input(send, "对方用户 ID", 19);
            FixedWidth(SocialUi.Button(send, "发送好友申请", SendRequest), 180);
            var accept = Row("AcceptRequest", requestPage);
            RequestInput = SocialUi.Input(accept, "收到的申请 ID", 19);
            FixedWidth(SocialUi.Button(accept, "接受申请", AcceptRequest), 180);
            SocialUi.Body(requestPage, "输入对方用户 ID 发送申请。将返回的申请 ID 告知接收者，由对方填写并接受。");
            SocialUi.Button(requestPage, "返回好友列表", Open);
            SocialUi.Button(navigation, "好友", Open);
        }
        private static RectTransform Row(string name, Transform parent, float height = 42)
        {
            var row = SocialUi.Row(name, parent, height);
            row.GetComponent<LayoutElement>().flexibleHeight = 0;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row;
        }
        private static void FixedWidth(Button button, float width)
        {
            var layout = button.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.flexibleWidth = 0;
        }
        public void OpenRequests()
        {
            if (!app.IsBusy) app.Show(requestPage);
        }
        public void Open()
        {
            if (app.IsBusy) return;
            app.Show(page);
            Refresh(pageNumber);
        }
        public void Refresh(int number) { if (number >= 1 && number <= 1000000) app.Run(Load(number)); }
        private IEnumerator Load(int number)
        {
            ApiResponse<FriendPage> response = null;
            yield return api.List(number, r => response = r);
            if (!app.RequireData(response)) yield break;
            if (response.Data.items == null) { app.SetStatus("好友列表响应无效。"); yield break; }
            if (number > 1 && response.Data.items.Length == 0)
            {
                yield return Load(Math.Max(1, (response.Data.total - 1) / 10 + 1));
                yield break;
            }
            pageNumber = number;
            Items = response.Data.items;
            pendingDelete = 0;
            deleteButtons.Clear();
            SocialUi.Clear(list);
            pageLabel.text = "共 " + response.Data.total + " 位好友 · 第 " + number + " 页";
            previous.interactable = number > 1;
            next.interactable = (long)number * 10 < response.Data.total;
            foreach (var friend in Items)
            {
                var row = Row("Friend_" + friend.friendId, list, 100);
                var identity = SocialUi.Button(row, friend.nickname + "\n用户名：" + friend.username, () => openChat(friend));
                identity.name = "FriendIdentity_" + friend.friendId;
                identity.GetComponent<LayoutElement>().minHeight = 100;
                identity.GetComponent<LayoutElement>().preferredHeight = 100;
                identity.GetComponent<Image>().color = SocialTheme.Surface;
                var label = identity.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                SocialUi.Stretch(label.rectTransform, 16, 6, 16, 6);
                FixedWidth(SocialUi.Button(row, "聊天", () => openChat(friend)), 80);
                var delete = SocialUi.Button(row, "删除好友", () => ConfirmDelete(friend));
                FixedWidth(delete, 112);
                deleteButtons.Add(friend.friendId, delete);
            }
            if (Items.Length == 0) SocialUi.Body(list, "还没有好友。点击上方“好友申请”，邀请同学加入。");
            app.SetStatus("好友列表已更新。点击好友的昵称或“聊天”开始交流。");
        }
        private void ConfirmDelete(FriendDto friend)
        {
            if (app.IsBusy) return;
            if (pendingDelete == friend.friendId) { app.Run(Delete(friend)); return; }
            foreach (var pair in deleteButtons) pair.Value.GetComponentInChildren<Text>().text = "删除好友";
            pendingDelete = friend.friendId;
            deleteButtons[friend.friendId].GetComponentInChildren<Text>().text = "确认删除";
            app.SetStatus("再次点击确认删除好友 " + friend.nickname + "；消息历史仍保留。");
        }
        public void SendRequest()
        {
            if (app.IsBusy || !app.TryId(ReceiverInput.text, out var id)) return;
            if (id == app.Auth.CurrentUser.id) { app.SetStatus("不能添加自己为好友。"); return; }
            app.Run(Request(id));
        }
        private IEnumerator Request(long id)
        {
            ApiResponse<FriendRequestDto> response = null;
            yield return api.Request(id, r => response = r);
            if (!app.RequireData(response)) yield break;
            LastRequestId = response.Data.requestId;
            app.SetStatus("申请已发送，申请 ID：" + LastRequestId + "。请告知接收者填写此 ID 并接受。");
        }
        public void AcceptRequest()
        {
            if (!app.IsBusy && app.TryId(RequestInput.text, out var id)) app.Run(Accept(id));
        }
        private IEnumerator Accept(long id)
        {
            ApiResponse<FriendRequestDto> response = null;
            yield return api.Accept(id, r => response = r);
            if (!app.RequireData(response)) yield break;
            RequestInput.text = "";
            yield return Load(1);
            if (app.Status.StartsWith("好友列表已更新")) app.SetStatus("已接受好友申请。返回好友列表即可开始聊天。");
        }
        private IEnumerator Delete(FriendDto friend)
        {
            ApiResponse<string> response = null;
            yield return api.Delete(friend.friendId, r => response = r);
            if (!app.Check(response)) yield break;
            app.Chat.MarkRemoved(friend);
            yield return Load(pageNumber);
        }
    }
}
