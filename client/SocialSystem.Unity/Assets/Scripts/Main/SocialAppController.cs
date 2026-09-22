using System.Collections;
using SocialSystem.Client.Auth;
using SocialSystem.Client.Network;
using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Main
{
    public sealed class SocialAppController : MonoBehaviour
    {
        private LoginSceneController login;
        private GameObject loginPanel;
        private RectTransform root;
        private RectTransform pages;
        private RectTransform navigation;
        private CanvasGroup controls;
        private Text status;
        public SocialSystem.Client.Friends.FriendsView Friends { get; private set; }
        public SocialSystem.Client.Messages.ChatView Chat { get; private set; }
        public SocialSystem.Client.Posts.PostsView Posts { get; private set; }
        public HomeView Home { get; private set; }
        public AuthManager Auth => login.authManager;
        public ApiClient Api => Auth.apiClient;
        public bool IsBusy { get; private set; }
        public bool IsOpen => root != null;
        public string Status => status == null ? "" : status.text;

        public void Initialize(LoginSceneController controller)
        {
            login = controller;
            loginPanel = login.usernameInput.transform.parent.gameObject;
        }
        public void Open()
        {
            if (IsOpen || Auth.CurrentUser == null || !Auth.tokenManager.HasToken) return;
            loginPanel.SetActive(false);
            root = SocialUi.Rect("SocialApp", transform);
            SocialUi.Stretch(root, 16, 12, 16, 12);
            root.gameObject.AddComponent<Image>().color = SocialTheme.Surface;
            controls = root.gameObject.AddComponent<CanvasGroup>();
            navigation = SocialUi.Row("Navigation", root);
            navigation.anchorMin = new Vector2(0, 1);
            navigation.anchorMax = Vector2.one;
            navigation.pivot = new Vector2(0.5f, 1);
            navigation.offsetMin = new Vector2(12, -54);
            navigation.offsetMax = new Vector2(-12, -12);
            pages = SocialUi.Rect("Pages", root);
            SocialUi.Stretch(pages, 0, 70, 0, 62);
            status = SocialUi.Label(root, "", 16);
            status.rectTransform.anchorMin = Vector2.zero;
            status.rectTransform.anchorMax = new Vector2(1, 0);
            status.rectTransform.pivot = new Vector2(0.5f, 0);
            status.rectTransform.offsetMin = new Vector2(14, 6);
            status.rectTransform.offsetMax = new Vector2(-14, 66);
            SocialUi.Button(navigation, "主页", () => Home.Open());
            Chat = new SocialSystem.Client.Messages.ChatView(this, navigation);
            Friends = new SocialSystem.Client.Friends.FriendsView(this, navigation, Chat.OpenFriend);
            Posts = new SocialSystem.Client.Posts.PostsView(this, navigation);
            SocialUi.Button(navigation, "退出登录", Logout);
            Home = new HomeView(this, Friends.Open, Chat.OpenPage, Posts.Open);
            Home.Open();
            SetStatus("已登录。");
        }
        public RectTransform CreatePage(string name)
        {
            var page = SocialUi.Column(name, pages);
            SocialUi.Stretch(page);
            page.gameObject.SetActive(false);
            return page;
        }
        public void Show(RectTransform page)
        {
            if (IsBusy) return;
            foreach (Transform child in pages) child.gameObject.SetActive(child == page);
            SetStatus("");
        }
        public void SetStatus(string message) { if (status != null) status.text = message; }
        public bool Check<T>(ApiResponse<T> response)
        {
            if (response != null && response.Success) return true;
            SetStatus(response?.Error ?? "请求未完成，请重试。");
            return false;
        }
        public bool RequireData<T>(ApiResponse<T> response) where T : class
        {
            if (!Check(response)) return false;
            if (response.Data != null) return true;
            SetStatus("服务器响应缺少数据，请重试。");
            return false;
        }
        public bool TryId(string text, out long id)
        {
            if (long.TryParse(text.Trim(), out id) && id > 0) return true;
            SetStatus("请输入有效的正整数 ID。");
            return false;
        }
        public void Run(IEnumerator operation)
        {
            if (!IsBusy && IsOpen) StartCoroutine(RunOperation(operation));
        }
        private IEnumerator RunOperation(IEnumerator operation)
        {
            IsBusy = true;
            controls.interactable = false;
            SetStatus("加载中…");
            try { yield return operation; }
            finally
            {
                IsBusy = false;
                if (controls != null) controls.interactable = true;
            }
        }
        private void Update()
        {
            if (IsOpen && !Auth.tokenManager.HasToken) Close("登录已失效，请重新登录。");
        }
        public void Logout() { if (!IsBusy) Close("已退出登录。"); }
        private void Close(string message)
        {
            StopAllCoroutines();
            IsBusy = false;
            Auth.Logout();
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = null;
            Friends = null;
            Chat = null;
            Posts = null;
            Home = null;
            loginPanel.SetActive(true);
            login.ResetLogin();
            login.statusText.text = message;
        }
    }
}
