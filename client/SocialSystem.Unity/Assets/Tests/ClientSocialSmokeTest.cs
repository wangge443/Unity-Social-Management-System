#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using SocialSystem.Client.Auth;
using SocialSystem.Client.Main;
using SocialSystem.Client.Network;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClientSocialSmokeTest : MonoBehaviour
{
    private LoginSceneController ui;
    private SocialAppController app;
    private AuthManager auth;
    private string username;
    private string password;
    private bool finished;
    private string testServer;
    private long firstAccount;
    private long secondAccount;
    private void ClearContactMetadata()
    {
        if (string.IsNullOrEmpty(testServer)) return;
        if (firstAccount > 0) SocialSystem.Client.Messages.ConversationStore.Clear(testServer, firstAccount);
        if (secondAccount > 0) SocialSystem.Client.Messages.ConversationStore.Clear(testServer, secondAccount);
    }
    private void Fail(string message)
    {
        finished = true;
        Debug.LogError("Client smoke test failed: " + message);
        ClearContactMetadata();
        EditorApplication.Exit(1);
    }
    private bool Check(bool condition, string message)
    {
        if (!condition) Fail(message);
        return condition;
    }
    private IEnumerator Timeout()
    {
        yield return new WaitForSecondsRealtime(150);
        if (!finished) Fail("Social flow timed out.");
    }
    private IEnumerator Idle()
    {
        while (ui.IsBusy || app.IsBusy) yield return null;
        yield return null;
    }
    private IEnumerator Login(string name)
    {
        ui.usernameInput.text = name;
        ui.passwordInput.text = password;
        ui.loginButton.onClick.Invoke();
        yield return Idle();
        Check(ui.LoginVerified && app.IsOpen && auth.tokenManager.HasToken, "Login / home");
        if (app.Home != null && auth.CurrentUser != null)
            Check(app.Home.IsVisible && app.Home.UsernameText.text == "用户名：" + auth.CurrentUser.username &&
                app.Home.NicknameText.text == "昵称：" + auth.CurrentUser.nickname &&
                app.Home.UserIdText.text == "用户 ID：" + auth.CurrentUser.id, "Home user fields / account switch");
    }
    private void Click(string title)
    {
        var button = app.GetComponentsInChildren<Button>()
            .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == title);
        if (button == null) { Fail("Missing button: " + title); return; }
        button.onClick.Invoke();
    }
    private void ClickNamed(string name)
    {
        var button = app.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
        if (button == null) { Fail("Missing named button: " + name); return; }
        button.onClick.Invoke();
    }
    private void CheckPostLayout()
    {
        var rects = app.GetComponentsInChildren<RectTransform>();
        var compose = rects.First(x => x.name == "ComposePost");
        var postPage = rects.First(x => x.name == "Posts");
        var feed = app.GetComponentsInChildren<ScrollRect>().First(x => x.name == "PostFeed");
        if (!Check(compose.rect.height <= 54 && feed.GetComponent<RectTransform>().rect.height >= postPage.rect.height * 0.60f,
            "Compact composer leaves most height for post feed")) return;
        foreach (var scroll in app.GetComponentsInChildren<SocialSystem.Client.Posts.PostCommentsScrollRect>())
            if (!Check(scroll.GetComponent<RectTransform>().rect.height <= 151, "Bounded comment viewport")) return;
        foreach (var text in app.GetComponentsInChildren<Text>().Where(x => x.name.StartsWith("PostBody_") || x.name.StartsWith("CommentBody_")))
            if (!Check(!text.supportRichText && text.horizontalOverflow == HorizontalWrapMode.Wrap &&
                text.rectTransform.rect.height + 2 >= text.preferredHeight, "Wrapped post/comment text has sufficient height")) return;
    }

    private IEnumerator Start()
    {
        StartCoroutine(Timeout());
        ui = FindAnyObjectByType<LoginSceneController>();
        username = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_USERNAME");
        password = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_PASSWORD");
        var url = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_URL");
        if (!Check(ui != null && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) &&
            !string.IsNullOrEmpty(url), "Configuration")) yield break;
        app = ui.App;
        auth = ui.authManager;
        auth.apiClient.baseUrl = url;
        if (!Check(ui.loginButton.GetComponentInChildren<Text>().text == "登录" &&
            ui.passwordInput.contentType == InputField.ContentType.Password &&
            ui.statusText.font == SocialSystem.Client.UI.SocialTheme.Font, "Chinese login presentation / password mask")) yield break;
        if (!app.IsOpen) ClientUiCapture.Save(ui.GetComponent<Canvas>(), "login");
        ApiResponse<UserResponse> first = null, second = null;
        yield return auth.Register(username, password, "甲 <b>原文</b>", r => first = r);
        yield return auth.Register(username + "b", password, "乙同学", r => second = r);
        if (!Check(first != null && first.Success && second != null && second.Success, "Register accounts")) yield break;
        var a = first.Data.id;
        testServer = url;
        firstAccount = a;
        var b = second.Data.id;
        secondAccount = b;
        yield return Login(username);
        if (!Check(auth.CurrentUser.id == a, "Current user")) yield break;
        Debug.Log("PASS: Social home login and identity.");
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "home");
        Click("进入好友");
        yield return Idle();
        if (!Check(!app.Home.IsVisible && app.Status.Contains("好友列表"), "Home friend entry")) yield break;
        Click("主页");
        Click("查看消息");
        yield return Idle();
        if (!Check(!app.Home.IsVisible && app.Chat.PeerId == 0, "Home message entry")) yield break;
        Click("主页");
        Click("浏览动态");
        yield return Idle();
        if (!Check(!app.Home.IsVisible && app.Status.Contains("动态列表"), "Home post entry")) yield break;
        Click("主页");
        Debug.Log("PASS: Social stage 9 home entries, user fields and Chinese login.");

        app.Friends.Open();
        yield return Idle();
        if (!Check(app.Friends.Items.Length == 0, "Empty friend list")) yield break;

        Click("添加好友");
        Click("搜索");
        if (!Check(!app.IsBusy && app.Status.Contains("请输入"), "Blank search validation")) yield break;
        app.Friends.SearchInput.text = username;
        Click("搜索");
        yield return Idle();
        if (!Check(app.Friends.SearchItems.Length == 1 && app.Friends.SearchItems[0].userId == b &&
            !app.Friends.SearchItems.Any(x => x.userId == a), "Search username excludes self")) yield break;
        app.Friends.SearchInput.text = "乙同学";
        Click("搜索");
        yield return Idle();
        if (!Check(app.Friends.SearchItems.Any(x => x.userId == b && x.nickname == "乙同学"), "Search nickname")) yield break;
        if (!Check(!app.GetComponentsInChildren<InputField>(true).Any(x => (x.placeholder as Text)?.text == "对方用户 ID"),
            "Manual receiver ID field removed")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "user-search");
        ClickNamed("AddFriend_" + b);
        yield return Idle();
        var requestId = app.Friends.LastRequestId;
        if (!Check(requestId > 0 && !app.Status.Contains("ID"), "Request sent without exposing ID")) yield break;
        var addButton = app.GetComponentsInChildren<Button>().First(x => x.name == "AddFriend_" + b);
        if (!Check(!addButton.interactable && addButton.GetComponentInChildren<Text>().text == "已申请",
            "Pending request disables duplicate adding")) yield break;
        addButton.onClick.Invoke();
        if (!Check(!app.IsBusy, "Duplicate click is ignored")) yield break;
        var friendApi = new SocialSystem.Client.Friends.FriendApi(auth.apiClient);
        ApiResponse<SocialSystem.Client.Friends.FriendRequestDto> duplicate = null;
        yield return friendApi.Request(b, r => duplicate = r);
        if (!Check(duplicate != null && duplicate.StatusCode == 409, "Server rejects duplicate request")) yield break;

        ApiResponse<SocialSystem.Client.Friends.FriendRequestDto> forbidden = null;
        yield return friendApi.Accept(requestId, r => forbidden = r);
        if (!Check(forbidden != null && forbidden.StatusCode == 403, "Sender cannot accept")) yield break;
        app.Logout();
        if (!Check(!app.IsOpen && !auth.tokenManager.HasToken && auth.CurrentUser == null, "Logout clears session")) yield break;
        yield return Login(username + "b");
        app.Friends.Open();
        yield return Idle();
        if (!Check(app.Friends.IncomingItems.Length == 1 && app.Friends.IncomingItems[0].requestId == requestId &&
            app.Friends.IncomingItems[0].username == username && app.Friends.IncomingItems[0].nickname == first.Data.nickname,
            "Opening friend page automatically shows recipient requests and sender identity")) yield break;
        var requestLabel = app.GetComponentsInChildren<Text>().First(x => x.name == "RequestIdentity_" + requestId);
        if (!Check(!requestLabel.supportRichText && requestLabel.text == first.Data.nickname + "\n用户名：" + username &&
            !app.GetComponentsInChildren<InputField>(true).Any(x => (x.placeholder as Text)?.text == "收到的申请 ID"),
            "Sender display is literal text and request ID input is removed")) yield break;
        app.Friends.OpenSearch();
        app.Friends.SearchInput.text = username;
        Click("搜索");
        yield return Idle();
        if (!Check(app.Friends.SearchItems.Single(x => x.userId == a).relationship == "incoming",
            "Reverse pending request is visible without allowing duplicate")) yield break;
        app.Friends.Open();
        yield return Idle();
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "incoming-requests");
        ClickNamed("AcceptRequest_" + requestId);
        yield return Idle();
        if (!Check(app.Friends.IncomingItems.Length == 0 && app.Friends.Items.Length == 1 &&
            app.Friends.Items[0].friendId == a, "Accept refreshes requests and friend list")) yield break;
        app.Friends.OpenSearch();
        app.Friends.SearchInput.text = username;
        Click("搜索");
        yield return Idle();
        var friendResult = app.GetComponentsInChildren<Button>().First(x => x.name == "AddFriend_" + a);
        if (!Check(!friendResult.interactable && friendResult.GetComponentInChildren<Text>().text == "已是好友",
            "Existing friendship disables add button")) yield break;
        app.Friends.SearchInput.text = "no_match_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        Click("搜索");
        yield return Idle();
        if (!Check(app.Friends.SearchItems.Length == 0 && app.GetComponentsInChildren<Text>().Any(x => x.text.Contains("未找到")),
            "Empty search clears previous results")) yield break;
        Debug.Log("PASS: Social friend requests, conflict, receiver authorization and list.");

        if (!Check(app.Chat.RecentCount == 0, "Recent contacts account isolation")) yield break;
        app.Friends.Open();
        yield return Idle();
        Canvas.ForceUpdateCanvases();
        var friendIdentity = app.GetComponentsInChildren<Button>().First(x => x.name == "FriendIdentity_" + a);
        if (!Check(friendIdentity.GetComponent<RectTransform>().rect.width > 300, "Friend identity has readable width")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "friends");
        ClickNamed("FriendIdentity_" + a);
        yield return Idle();
        if (!Check(app.Chat.PeerId == a && app.Chat.PeerUsername == username &&
            app.Chat.PeerNickname == first.Data.nickname, "Friend identity click passes exact contact")) yield break;
        app.Chat.ContentInput.text = "你好，甲！<b>按原文显示</b>";
        app.Chat.Send();
        yield return Idle();
        if (!Check(app.Chat.Items.Length == 1 && app.Chat.Items[0].senderId == b, "First message")) yield break;
        app.Logout();
        yield return Login(username);
        app.Friends.Open();
        yield return Idle();
        Click("聊天");
        yield return Idle();
        if (!Check(app.Chat.PeerId == b && app.Chat.Items.Length == 1, "Friend chat entry")) yield break;
        app.Chat.ContentInput.text = "   ";
        app.Chat.Send();
        if (!Check(!app.IsBusy && app.Status.Contains("不能为空"), "Empty message")) yield break;
        app.Chat.ContentInput.text = "你好，乙！";
        app.Chat.Send();
        yield return Idle();
        if (!Check(app.Chat.Items.Length == 2 && app.Chat.Items[0].id < app.Chat.Items[1].id &&
            app.Chat.Items[1].senderId == a, "Two-way history ordering")) yield break;
        Canvas.ForceUpdateCanvases();
        var sidebar = app.GetComponentsInChildren<RectTransform>().First(x => x.name == "ConversationList");
        var conversation = app.GetComponentsInChildren<RectTransform>().First(x => x.name == "Conversation");
        if (!Check(sidebar.rect.width < conversation.rect.width, "Chat content wider than conversation selector")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "chat");
        var receivedRow = app.GetComponentsInChildren<HorizontalLayoutGroup>().FirstOrDefault(x => x.name == "Message_" + app.Chat.Items[0].id);
        var sentRow = app.GetComponentsInChildren<HorizontalLayoutGroup>().FirstOrDefault(x => x.name == "Message_" + app.Chat.Items[1].id);
        if (!Check(receivedRow != null && receivedRow.childAlignment == TextAnchor.UpperLeft &&
            sentRow != null && sentRow.childAlignment == TextAnchor.UpperRight, "Message bubble directions")) yield break;
        app.Chat.ContentInput.text = new string('长', 1997) + "\n结束";
        app.Chat.Send();
        yield return Idle();
        if (!Check(app.Chat.Items.Length == 3 && app.Chat.Items[2].content.Length == 2000, "Long multiline message boundary")) yield break;
        var historyScroll = app.GetComponentsInChildren<ScrollRect>().First(x => x.transform.parent.name == "Conversation");
        if (!Check(Mathf.Abs(historyScroll.verticalNormalizedPosition) < 0.01f, "Sending keeps latest message in view")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "chat-long");
        Debug.Log("PASS: Social chat send, history, ordering, bubble directions and 2000-character input.");

        app.Posts.Open();
        yield return Idle();
        app.Posts.ContentInput.text = " ";
        app.Posts.Publish();
        if (!Check(!app.IsBusy && app.Status.Contains("不能为空"), "Empty post")) yield break;
        app.Posts.ContentInput.text = "课程动态：中文 / <b>纯文本</b>\n第二行";
        app.Posts.Publish();
        yield return Idle();
        var postId = app.Posts.LastPublishedId;
        if (!Check(postId > 0 && app.Posts.Items.Length == 1, "Publish / list")) yield break;
        ClickNamed("ToggleComments_" + postId);
        yield return Idle();
        if (!Check(app.Posts.CommentsExpanded(postId) && app.Posts.CommentItems(postId).Length == 0 &&
            app.GetComponentsInChildren<Text>().Any(x => x.text == "暂无评论"), "Empty expanded comments")) yield break;
        ClickNamed("ToggleComments_" + postId);
        if (!Check(!app.Posts.CommentsExpanded(postId), "Collapse empty comments")) yield break;

        app.Posts.ToggleLike(app.Posts.Items[0]);
        yield return Idle();
        if (!Check(app.Posts.Items[0].isLikedByMe && app.Posts.Items[0].likeCount == 1, "Like")) yield break;
        app.Posts.ToggleLike(app.Posts.Items[0]);
        yield return Idle();
        if (!Check(!app.Posts.Items[0].isLikedByMe && app.Posts.Items[0].likeCount == 0, "Unlike")) yield break;
        app.Posts.Comment(postId, "第一条评论");
        yield return Idle();
        if (!Check(app.Posts.Items[0].commentCount == 1, "Comment count")) yield break;
        app.Logout();
        yield return Login(username + "b");
        app.Posts.Open();
        yield return Idle();
        if (!Check(app.Posts.Items.Length == 1 && !app.GetComponentsInChildren<Button>()
            .Any(button => button.gameObject.activeInHierarchy &&
                button.GetComponentInChildren<Text>()?.text == "删除动态"), "Other author delete hidden")) yield break;
        app.Posts.Comment(postId, "乙的评论");
        yield return Idle();
        if (!Check(app.Posts.CommentsExpanded(postId) && app.Posts.CommentItems(postId).Any(x =>
            x.author.id == b && x.content == "乙的评论"), "Submitting automatically refreshes own comment and count")) yield break;
        ClickNamed("ToggleComments_" + postId);
        if (!Check(!app.Posts.CommentsExpanded(postId), "Collapse comments")) yield break;
        ClickNamed("ToggleComments_" + postId);
        yield return Idle();
        if (!Check(app.Posts.CommentItems(postId).Any(x => x.author.username == username + "b" && x.content == "乙的评论"),
            "B expands and reads stored comment")) yield break;

        app.Posts.ToggleLike(app.Posts.Items[0]);
        yield return Idle();
        app.Logout();
        yield return Login(username);
        app.Posts.Open();
        yield return Idle();
        if (!Check(app.Posts.Items[0].likeCount == 1 && !app.Posts.Items[0].isLikedByMe &&
            app.Posts.Items[0].commentCount == 2, "Cross-user counts / own like state")) yield break;
        ClickNamed("ToggleComments_" + postId);
        yield return Idle();
        if (!Check(app.Posts.CommentItems(postId).Any(x => x.author.id == b && x.content == "乙的评论"),
            "A reads B comment after account switch")) yield break;
        var postApi = new SocialSystem.Client.Posts.PostApi(auth.apiClient);
        for (var i = 0; i < 9; i++)
        {
            ApiResponse<SocialSystem.Client.Posts.CommentDto> extraComment = null;
            yield return postApi.Comment(postId, "多条评论 " + i + "：" + new string('评', 80) + "\n<b>纯文本</b>", r => extraComment = r);
            if (!Check(extraComment != null && extraComment.Success, "Multiple comments setup")) yield break;
        }
        app.Posts.Refresh(1);
        yield return Idle();
        if (!Check(app.Posts.Items[0].commentCount == 11 && app.Posts.CommentItems(postId).Length == 10,
            "Comment count and first page")) yield break;
        ClickNamed("CommentNext_" + postId);
        yield return Idle();
        if (!Check(app.Posts.CommentItems(postId).Length == 1 && app.Posts.CommentItems(postId)[0].content == "第一条评论",
            "Comment pagination to oldest page")) yield break;
        ClickNamed("CommentPrevious_" + postId);
        yield return Idle();
        ApiResponse<SocialSystem.Client.Posts.PostDto> extraPost = null;
        yield return postApi.Publish("第二张卡片，用于验证动态列表滚动。\n" + new string('文', 160), r => extraPost = r);
        if (!Check(extraPost != null && extraPost.Success, "Feed scrolling fixture")) yield break;
        app.Posts.Refresh(1);
        yield return Idle();
        ClientUiCapture.SavePosts(ui.GetComponent<Canvas>(), "posts-comments", CheckPostLayout);
        var feedScroll = app.GetComponentsInChildren<ScrollRect>().First(x => x.name == "PostFeed");
        var commentScroll = app.GetComponentsInChildren<SocialSystem.Client.Posts.PostCommentsScrollRect>().Single();
        if (!Check(feedScroll.content.rect.height > feedScroll.viewport.rect.height &&
            commentScroll.content.rect.height > commentScroll.viewport.rect.height, "Independent scroll ranges")) yield break;
        feedScroll.verticalNormalizedPosition = 1;
        commentScroll.verticalNormalizedPosition = 0;
        commentScroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { scrollDelta = new Vector2(0, -1) });
        if (!Check(feedScroll.verticalNormalizedPosition < 1, "Comment boundary wheel scrolls outer feed")) yield break;
        feedScroll.verticalNormalizedPosition = 0;
        commentScroll.verticalNormalizedPosition = 1;
        ClientUiCapture.SavePosts(ui.GetComponent<Canvas>(), "posts-scrolled", CheckPostLayout);
        // Fail only the comment request, then restore and retry by reopening.
        ClickNamed("ToggleComments_" + postId);
        auth.apiClient.baseUrl = "http://127.0.0.1:1";
        ClickNamed("ToggleComments_" + postId);
        yield return Idle();
        if (!Check(app.Status.Contains("加载失败") && app.IsOpen && !app.IsBusy, "Comment load failure is recoverable")) yield break;
        auth.apiClient.baseUrl = url;
        Click("重试");
        yield return Idle();
        if (!Check(app.Posts.CommentItems(postId).Length == 10, "Comment retry")) yield break;
        ApiResponse<string> removeExtra = null;
        yield return postApi.Delete(extraPost.Data.id, r => removeExtra = r);
        if (!Check(removeExtra != null && removeExtra.Success, "Remove feed fixture")) yield break;
        app.Posts.Refresh(1);
        yield return Idle();
        Debug.Log("PASS: Social stored comments cross-user, auto-refresh, empty state, expansion, pagination, nested scroll and three-resolution post layout.");

        Click("删除动态");
        if (!Check(!app.IsBusy && app.Posts.Items.Length == 1, "Delete confirmation")) yield break;
        Click("确认删除");
        yield return Idle();
        if (!Check(app.Posts.Items.Length == 0, "Delete own post")) yield break;
        Debug.Log("PASS: Social posts, comments, likes, unlike, ownership and delete.");

        app.Friends.Open();
        yield return Idle();
        Click("删除好友");
        Click("确认删除");
        yield return Idle();
        if (!Check(app.Friends.Items.Length == 0, "Delete friend")) yield break;
        app.Logout();
        yield return Login(username);
        app.Chat.OpenPage();
        yield return Idle();
        if (!Check(app.Chat.RecentCount == 1, "Recent contact survives relogin after removal")) yield break;
        ClickNamed("RecentContact_" + b);
        yield return Idle();
        if (!Check(app.Chat.Items.Length == 3, "History survives removal")) yield break;
        app.Chat.ContentInput.text = "不应发送";
        app.Chat.Send();
        yield return Idle();
        if (!Check(app.Status.Contains("仅好友") && app.Chat.ContentInput.text == "不应发送", "Nonfriend send denied")) yield break;

        if (!Check(!app.Chat.ContentInput.interactable, "Removed contact is read only")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "chat-history");
        app.Logout();
        yield return Login(username + "b");
        app.Chat.OpenPage();
        yield return Idle();
        ClickNamed("RecentContact_" + a);
        yield return Idle();
        app.Chat.ContentInput.text = "对方删除好友后应拒绝发送";
        app.Chat.Send();
        yield return Idle();
        if (!Check(app.Status.Contains("仅好友") && !app.Chat.ContentInput.interactable &&
            app.Chat.Items.Length == 3, "Remote removal rejects send but keeps history")) yield break;
        app.Logout();
        yield return Login(username);
        Debug.Log("PASS: Social round 2 friend clicks, persisted contact history, account isolation and remote removal.");
        auth.apiClient.baseUrl = "http://127.0.0.1:1";
        auth.apiClient.timeoutSeconds = 2;
        app.Friends.Open();
        yield return Idle();
        if (!Check(app.Status.Contains("无法连接") && app.IsOpen && !app.IsBusy, "Offline recovery")) yield break;
        auth.apiClient.baseUrl = url;
        if (!Check(ui.loginButton.GetComponentInChildren<Text>().text == "登录" &&
            ui.passwordInput.contentType == InputField.ContentType.Password &&
            ui.statusText.font == SocialSystem.Client.UI.SocialTheme.Font, "Chinese login presentation / password mask")) yield break;
        if (!app.IsOpen) ClientUiCapture.Save(ui.GetComponent<Canvas>(), "login");
        auth.apiClient.timeoutSeconds = 15;
        app.Friends.Refresh(1);
        yield return Idle();
        if (!Check(app.Status.Contains("已更新"), "Network restored")) yield break;
        auth.tokenManager.Save("invalid-token", DateTimeOffset.UtcNow.AddMinutes(1));
        app.Friends.Refresh(1);
        yield return Idle();
        if (!Check(!app.IsOpen && !auth.tokenManager.HasToken && auth.CurrentUser == null, "401 returns to login")) yield break;
        yield return Login(username);
        auth.tokenManager.Save(auth.tokenManager.AccessToken, DateTimeOffset.UtcNow.AddMilliseconds(100));
        yield return new WaitForSecondsRealtime(0.25f);
        if (!Check(!app.IsOpen && auth.CurrentUser == null, "Local expiry")) yield break;
        ui.usernameInput.text = username;
        ui.passwordInput.text = "wrong-password";
        ui.loginButton.onClick.Invoke();
        yield return Idle();
        if (!Check(!ui.LoginVerified && !auth.tokenManager.HasToken && !app.IsOpen, "Wrong password")) yield break;
        yield return Login(username);
        app.Logout();
        Debug.Log("PASS: Social logout, relogin, network failure, recovery, 401, expiry and wrong password.");

        Debug.Log("PASS: Social user search, nickname matching, hidden IDs, pending states and search-to-chat lifecycle.");
        // Another pair tests rejection, multiple incoming rows and absence of friendship.
        ApiResponse<UserResponse> third = null, fourth = null;
        yield return auth.Register(username + "c", password, "丙同学", r => third = r);
        yield return auth.Register(username + "d", password, "丁同学", r => fourth = r);
        if (!Check(third != null && third.Success && fourth != null && fourth.Success, "Rejection accounts")) yield break;
        yield return Login(username + "c");
        app.Friends.OpenSearch();
        app.Friends.SearchInput.text = username + "d";
        Click("搜索");
        yield return Idle();
        ClickNamed("AddFriend_" + fourth.Data.id);
        yield return Idle();
        var rejectId = app.Friends.LastRequestId;
        app.Logout();
        yield return Login(username);
        app.Friends.OpenSearch();
        app.Friends.SearchInput.text = username + "d";
        Click("搜索");
        yield return Idle();
        ClickNamed("AddFriend_" + fourth.Data.id);
        yield return Idle();
        var anotherId = app.Friends.LastRequestId;
        app.Logout();
        yield return Login(username + "d");
        app.Friends.Open();
        yield return Idle();
        if (!Check(app.Friends.IncomingItems.Length == 2 && app.Friends.Items.Length == 0,
            "Multiple incoming requests automatically visible")) yield break;
        Canvas.ForceUpdateCanvases();
        if (!Check(app.GetComponentsInChildren<Button>().Count(x => x.name.StartsWith("RejectRequest_")) == 2,
            "Each incoming request has its own action")) yield break;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "incoming-multiple");
        var incomingScroll = app.GetComponentsInChildren<ScrollRect>().Single();
        if (!Check(incomingScroll.content.rect.height > incomingScroll.viewport.rect.height, "Multiple requests can scroll")) yield break;
        incomingScroll.verticalNormalizedPosition = 0;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "incoming-multiple-bottom");
        ClickNamed("RejectRequest_" + rejectId);
        yield return Idle();
        if (!Check(app.Friends.IncomingItems.Length == 1 && app.Friends.IncomingItems[0].requestId == anotherId &&
            app.Friends.Items.Length == 0, "Reject removes only chosen request without friendship")) yield break;
        // A stale action (another device already handled it) refreshes without adding a friend.
        app.Friends.HandleRequest(rejectId, true);
        yield return Idle();
        if (!Check(app.Status.Contains("已处理") && !app.Status.Contains("ID") && app.Friends.IncomingItems.Length == 1, "Stale request recovery")) yield break;
        ClickNamed("RejectRequest_" + anotherId);
        yield return Idle();
        if (!Check(app.Friends.IncomingItems.Length == 0 && app.Friends.Items.Length == 0, "Reject final row / empty state")) yield break;
        app.Logout();
        yield return Login(username + "c");
        app.Friends.Open();
        yield return Idle();
        if (!Check(app.Friends.Items.Length == 0, "Rejected sender has no friendship")) yield break;
        app.Logout();
        Debug.Log("PASS: Social received-request identity, automatic accept refresh, multiple rows, rejection, stale actions and empty state.");

        finished = true;
        Debug.Log("PASS: Unity social modules end-to-end through isolated HTTP API and MySQL.");
        ClearContactMetadata();
        EditorApplication.Exit(0);
    }
}
#endif
