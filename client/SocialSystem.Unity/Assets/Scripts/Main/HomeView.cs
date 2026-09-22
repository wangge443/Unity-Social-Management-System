using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SocialSystem.Client.Main
{
    public sealed class HomeView
    {
        private readonly SocialAppController app;
        private readonly RectTransform page;
        public Text UsernameText { get; }
        public Text NicknameText { get; }
        public Text UserIdText { get; }
        public bool IsVisible => page.gameObject.activeInHierarchy;

        public HomeView(SocialAppController app, UnityAction friends, UnityAction messages, UnityAction posts)
        {
            this.app = app;
            page = app.CreatePage("Home");
            var content = SocialUi.Scroll(page);
            SocialUi.Label(content, "我的主页", 28, 44);
            var subtitle = SocialUi.Label(content, "从这里开始，和好友保持联系。", SocialTheme.SmallSize, 28);
            subtitle.color = SocialTheme.Muted;
            var card = SocialUi.Column("UserCard", content);
            card.gameObject.AddComponent<Image>().color = SocialTheme.Surface;

            NicknameText = SocialUi.Body(card, "");
            UsernameText = SocialUi.Body(card, "");
            UserIdText = SocialUi.Body(card, "");
            var entries = SocialUi.Row("QuickEntries", content, 52);
            SocialUi.Button(entries, "进入好友", friends);
            SocialUi.Button(entries, "查看消息", messages);
            SocialUi.Button(entries, "浏览动态", posts);
        }
        public void Open()
        {
            if (app.IsBusy || app.Auth.CurrentUser == null) return;
            var user = app.Auth.CurrentUser;
            NicknameText.text = "昵称：" + user.nickname;
            UsernameText.text = "用户名：" + user.username;
            UserIdText.text = "用户 ID：" + user.id;
            app.Show(page);
        }
    }
}
