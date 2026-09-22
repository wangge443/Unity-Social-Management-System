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
        Click("好友申请");
        app.Friends.ReceiverInput.text = a.ToString();
        Click("发送好友申请");
        if (!Check(!app.IsBusy && app.Status.Contains("自己"), "Self request validation")) yield break;
        app.Friends.ReceiverInput.text = b.ToString();
        Click("发送好友申请");
        yield return Idle();
        var requestId = app.Friends.LastRequestId;
        if (!Check(requestId > 0, "Friend request")) yield break;
        Click("发送好友申请");
        yield return Idle();
        if (!Check(app.Status.Contains("待处理"), "Duplicate friend request")) yield break;
        app.Friends.OpenRequests();
        app.Friends.RequestInput.text = requestId.ToString();
        Click("接受申请");
        yield return Idle();
        if (!Check(app.Status.Contains("接收者"), "Sender cannot accept")) yield break;
        app.Logout();
        if (!Check(!app.IsOpen && !auth.tokenManager.HasToken && auth.CurrentUser == null, "Logout clears session")) yield break;
        yield return Login(username + "b");
        app.Friends.Open();
        yield return Idle();
        app.Friends.OpenRequests();
        app.Friends.RequestInput.text = requestId.ToString();
        Click("接受申请");
        yield return Idle();
        if (!Check(app.Friends.Items.Length == 1 && app.Friends.Items[0].friendId == a, "Accept / list")) yield break;
        Debug.Log("PASS: Social friend requests, conflict, receiver authorization and list.");

        if (!Check(app.Chat.RecentCount == 0, "Recent contacts account isolation")) yield break;
        Click("返回好友列表");
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
        app.Posts.ToggleLike(app.Posts.Items[0]);
        yield return Idle();
        app.Logout();
        yield return Login(username);
        app.Posts.Open();
        yield return Idle();
        if (!Check(app.Posts.Items[0].likeCount == 1 && !app.Posts.Items[0].isLikedByMe &&
            app.Posts.Items[0].commentCount == 2, "Cross-user counts / own like state")) yield break;
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
        finished = true;
        Debug.Log("PASS: Unity social modules end-to-end through isolated HTTP API and MySQL.");
        ClearContactMetadata();
        EditorApplication.Exit(0);
    }
}
#endif
