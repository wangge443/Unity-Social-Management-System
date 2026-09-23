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
        private readonly RectTransform requestList;
        private readonly Text pageLabel;
        private readonly Button previous;
        private readonly Button next;
        private readonly Action<FriendDto> openChat;
        private readonly Dictionary<long, Button> deleteButtons = new Dictionary<long, Button>();
        private int pageNumber = 1;
        private int requestPageNumber = 1;
        private int requestTotal;
        private bool friendsLoaded;
        private bool requestsLoaded;
        private long pendingDelete;
        private readonly RectTransform searchPage;
        private readonly RectTransform searchList;
        private string searchKeyword;
        private int searchNumber = 1;
        private int searchTotal;
        public InputField SearchInput { get; }
        public UserSearchDto[] SearchItems { get; private set; } = Array.Empty<UserSearchDto>();
        public FriendDto[] Items { get; private set; } = Array.Empty<FriendDto>();
        public IncomingFriendRequestDto[] IncomingItems { get; private set; } = Array.Empty<IncomingFriendRequestDto>();
        public long LastRequestId { get; private set; }

        public FriendsView(SocialAppController app, Transform navigation, Action<FriendDto> openChat)
        {
            this.app = app;
            this.openChat = openChat;
            api = new FriendApi(app.Api);
            page = app.CreatePage("Friends");
            var header = Row("Header", page);
            SocialUi.Label(header, "我的好友", 24);
            FixedWidth(SocialUi.Button(header, "添加好友", OpenSearch), 130);
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
            var requestHeader = Row("RequestHeader", requestPage);
            SocialUi.Label(requestHeader, "好友申请", 24);
            FixedWidth(SocialUi.Button(requestHeader, "返回好友列表", Open), 180);
            FixedWidth(SocialUi.Button(requestPage, "搜索添加好友", OpenSearch), 180);
            requestList = SocialUi.Scroll(requestPage);
            searchPage = app.CreatePage("UserSearch");
            var searchHeader = Row("SearchHeader", searchPage);
            SocialUi.Label(searchHeader, "搜索添加好友", 24);
            FixedWidth(SocialUi.Button(searchHeader, "返回好友列表", Open), 150);
            FixedWidth(SocialUi.Button(searchHeader, "好友申请", OpenRequests), 120);
            var searchRow = Row("UserSearchInput", searchPage);
            SearchInput = SocialUi.Input(searchRow, "输入用户名或昵称", 32);
            FixedWidth(SocialUi.Button(searchRow, "搜索", () => Search()), 100);
            searchList = SocialUi.Scroll(searchPage);
            SocialUi.Body(searchList, "输入用户名或昵称，搜索你想添加的好友。");
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
            if (app.IsBusy) return;
            app.Show(requestPage);
            app.Run(LoadIncoming(requestPageNumber));
        }
        public void Open()
        {
            if (app.IsBusy) return;
            app.Show(page);
            Refresh(pageNumber);
        }
        public void Refresh(int number)
        {
            if (number >= 1 && number <= 1000000) app.Run(LoadAll(number, requestPageNumber));
        }
        public void RefreshRequests(int number)
        {
            if (number >= 1 && number <= 1000000) app.Run(LoadIncoming(number));
        }
        private IEnumerator LoadAll(int friendPage, int incomingPage)
        {
            yield return Load(friendPage);
            if (!friendsLoaded) yield break;
            yield return LoadIncoming(incomingPage);
            if (requestsLoaded) app.SetStatus("好友列表与收到的申请已更新，可滚动查看更多。点击好友昵称或“聊天”开始交流。");
        }
        private IEnumerator Load(int number)
        {
            friendsLoaded = false;
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
            pageLabel.text = "共 " + response.Data.total + " 位好友 · 第 " + number + " 页";
            previous.interactable = number > 1;
            next.interactable = (long)number * 10 < response.Data.total;
            friendsLoaded = true;
            RenderFriends();
            app.SetStatus("好友列表已更新。");
        }
        private IEnumerator LoadIncoming(int number)
        {
            requestsLoaded = false;
            ApiResponse<IncomingFriendRequestPage> response = null;
            yield return api.Incoming(number, r => response = r);
            if (!app.RequireData(response) || response.Data.items == null)
            {
                if (response != null && response.Success) app.SetStatus("好友申请列表响应无效。");
                IncomingItems = Array.Empty<IncomingFriendRequestDto>();
                RenderAll();
                yield break;
            }
            if (number > 1 && response.Data.items.Length == 0)
            {
                yield return LoadIncoming(Math.Max(1, (response.Data.total - 1) / 10 + 1));
                yield break;
            }
            requestPageNumber = number;
            requestTotal = response.Data.total;
            IncomingItems = response.Data.items;
            requestsLoaded = true;
            RenderAll();
            app.SetStatus("收到的好友申请已更新。");
        }
        private void RenderAll()
        {
            RenderFriends();
            SocialUi.Clear(requestList);
            RenderRequests(requestList);
        }
        private void RenderRequests(Transform parent)
        {
            SocialUi.Label(parent, "收到的好友申请", 22, 38);
            var paging = Row("RequestPaging", parent, 38);
            SocialUi.Label(paging, requestsLoaded ? "共 " + requestTotal + " 条 · 第 " + requestPageNumber + " 页" : "申请列表未加载", 16);
            var prev = SocialUi.Button(paging, "申请上一页", () => RefreshRequests(requestPageNumber - 1));
            var following = SocialUi.Button(paging, "申请下一页", () => RefreshRequests(requestPageNumber + 1));
            FixedWidth(prev, 112);
            FixedWidth(following, 112);
            prev.interactable = requestsLoaded && requestPageNumber > 1;
            following.interactable = requestsLoaded && (long)requestPageNumber * 10 < requestTotal;
            FixedWidth(SocialUi.Button(paging, "刷新申请", () => RefreshRequests(requestPageNumber)), 112);
            foreach (var request in IncomingItems)
            {
                var row = Row("IncomingRequest_" + request.requestId, parent, 84);
                var identity = SocialUi.Label(row, request.nickname + "\n用户名：" + request.username, 18, 84);
                identity.name = "RequestIdentity_" + request.requestId;
                var accept = SocialUi.Button(row, "同意", () => HandleRequest(request.requestId, true));
                accept.name = "AcceptRequest_" + request.requestId;
                FixedWidth(accept, 88);
                var reject = SocialUi.Button(row, "拒绝", () => HandleRequest(request.requestId, false));
                reject.name = "RejectRequest_" + request.requestId;
                FixedWidth(reject, 88);
            }
            if (IncomingItems.Length == 0)
                SocialUi.Body(parent, requestsLoaded ? "暂无待处理的好友申请。" : "请刷新申请列表。");
        }
        private void RenderFriends()
        {
            pendingDelete = 0;
            deleteButtons.Clear();
            SocialUi.Clear(list);
            // A shared scroll area supports any number of requests without squeezing friend rows.
            RenderRequests(list);
            SocialUi.Label(list, "好友列表", 22, 38);
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
            if (Items.Length == 0) SocialUi.Body(list, "还没有好友。点击上方“添加好友”，搜索用户名或昵称。");
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

        public void OpenSearch()
        {
            if (app.IsBusy) return;
            app.Show(searchPage);
            if (!string.IsNullOrEmpty(searchKeyword)) Search(searchNumber);
        }
        public void Search(int number = 1)
        {
            if (app.IsBusy || number < 1 || number > 1000000) return;
            var keyword = SearchInput.text.Trim();
            if (keyword.Length == 0 || keyword.Length > 32)
            {
                SearchItems = Array.Empty<UserSearchDto>();
                SocialUi.Clear(searchList);
                app.SetStatus("请输入用户名或昵称（最多 32 字符）。");
                return;
            }
            if (keyword != searchKeyword) number = 1;
            app.Run(LoadSearch(keyword, number));
        }
        private IEnumerator LoadSearch(string keyword, int number)
        {
            // Clear old actionable results before a new request, including on failure.
            SearchItems = Array.Empty<UserSearchDto>();
            SocialUi.Clear(searchList);
            ApiResponse<UserSearchPage> response = null;
            yield return api.Search(keyword, number, r => response = r);
            if (!app.RequireData(response) || response.Data.items == null) yield break;
            if (number > 1 && response.Data.items.Length == 0)
            {
                yield return LoadSearch(keyword, Math.Max(1, (response.Data.total - 1) / 10 + 1));
                yield break;
            }
            searchKeyword = keyword;
            searchNumber = number;
            searchTotal = response.Data.total;
            SearchItems = response.Data.items;
            RenderSearch();
            app.SetStatus("搜索完成。点击添加好友发送申请；对方已申请时，请到好友申请列表处理。");
        }
        private void RenderSearch()
        {
            SocialUi.Clear(searchList);
            var paging = Row("SearchPaging", searchList, 38);
            SocialUi.Label(paging, "共 " + searchTotal + " 位 · 第 " + searchNumber + " 页", 16);
            var prev = SocialUi.Button(paging, "上一页", () => Search(searchNumber - 1));
            var nextPage = SocialUi.Button(paging, "下一页", () => Search(searchNumber + 1));
            FixedWidth(prev, 88);
            FixedWidth(nextPage, 88);
            prev.interactable = searchNumber > 1;
            nextPage.interactable = (long)searchNumber * 10 < searchTotal;
            foreach (var user in SearchItems)
            {
                var row = Row("SearchUser_" + user.userId, searchList, 84);
                SocialUi.Label(row, user.nickname + "\n用户名：" + user.username, 18, 84);
                var title = user.relationship == "friends" ? "已是好友" :
                    user.relationship == "outgoing" ? "已申请" :
                    user.relationship == "incoming" ? "对方已申请" : "添加好友";
                var button = SocialUi.Button(row, title, () => AddFriend(user));
                button.name = "AddFriend_" + user.userId;
                FixedWidth(button, 140);
                button.interactable = user.relationship == "none";
            }
            if (SearchItems.Length == 0) SocialUi.Body(searchList, "未找到匹配用户，请尝试其他用户名或昵称。");
        }
        private void AddFriend(UserSearchDto user)
        {
            if (app.IsBusy || user.relationship != "none") return;
            app.Run(Request(user));
        }
        private IEnumerator Request(UserSearchDto user)
        {
            ApiResponse<FriendRequestDto> response = null;
            yield return api.Request(user.userId, r => response = r);
            if (!app.RequireData(response))
            {
                var error = app.Status;
                if (response != null && (response.StatusCode == 409 || response.StatusCode == 404))
                {
                    yield return LoadSearch(searchKeyword, searchNumber);
                    app.SetStatus(error + " 已刷新搜索结果。");
                }
                yield break;
            }
            LastRequestId = response.Data.requestId;
            user.relationship = "outgoing";
            RenderSearch();
            app.SetStatus("好友申请已发送，等待对方处理。");
        }

        public void HandleRequest(long requestId, bool accept)
        {
            if (!app.IsBusy) app.Run(Handle(requestId, accept));
        }
        private IEnumerator Handle(long id, bool accept)
        {
            ApiResponse<FriendRequestDto> response = null;
            if (accept) yield return api.Accept(id, r => response = r);
            else yield return api.Reject(id, r => response = r);
            if (!app.RequireData(response))
            {
                var error = response != null && response.StatusCode == 409 ? "该申请已处理或双方已是好友。" :
                    response != null && response.StatusCode == 404 ? "该申请已不存在。" :
                    response != null && response.StatusCode == 403 ? "只能处理发送给自己的申请。" : app.Status;
                if (response != null && (response.StatusCode == 409 || response.StatusCode == 404 || response.StatusCode == 403))
                {
                    yield return LoadAll(pageNumber, requestPageNumber);
                    app.SetStatus(error + " 已尝试刷新好友和申请列表。");
                }
                yield break;
            }
            IncomingItems = Array.FindAll(IncomingItems, x => x.requestId != id);
            requestTotal = Math.Max(0, requestTotal - 1);
            RenderAll();
            yield return LoadAll(1, requestPageNumber);
            var result = accept ? "已同意好友申请。" : "已拒绝好友申请。";
            app.SetStatus(friendsLoaded && requestsLoaded ? result + "好友与申请列表已更新。" :
                result + "列表刷新未完成，请点击刷新重试。");
        }
        private IEnumerator Delete(FriendDto friend)
        {
            ApiResponse<string> response = null;
            yield return api.Delete(friend.friendId, r => response = r);
            if (!app.Check(response)) yield break;
            app.Chat.MarkRemoved(friend);
            yield return LoadAll(pageNumber, requestPageNumber);
        }
    }
}
