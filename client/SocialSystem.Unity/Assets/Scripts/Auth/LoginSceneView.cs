using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Auth
{
    // Presentation only: retain the scene's serialized controls and event wiring.
    public static class LoginSceneView
    {
        public static void Apply(LoginSceneController owner)
        {
            var panel = owner.usernameInput.transform.parent.GetComponent<RectTransform>();
            Place(panel, 0, new Vector2(480, 490));
            panel.GetComponent<Image>().color = SocialTheme.Surface;
            var camera = Camera.main;
            if (camera != null) camera.backgroundColor = SocialTheme.Background;

            var title = panel.Find("Title").GetComponent<Text>();
            SocialTheme.Text(title, 30);
            title.text = "SocialSystem";
            Place(title.rectTransform, 190, new Vector2(408, 48));
            var subtitle = panel.Find("Server").GetComponent<Text>();
            SocialTheme.Text(subtitle, SocialTheme.SmallSize);
            subtitle.color = SocialTheme.Muted;
            subtitle.text = "连接好友，分享校园生活";
            Place(subtitle.rectTransform, 146, new Vector2(408, 30));

            Caption(panel, "UsernameCaption", "用户名", 100);
            ConfigureInput(owner.usernameInput, "请输入用户名", 60);
            Caption(panel, "PasswordCaption", "密码", 5);
            ConfigureInput(owner.passwordInput, "请输入密码", -35);
            owner.passwordInput.contentType = InputField.ContentType.Password;

            Place(owner.loginButton.GetComponent<RectTransform>(), -100, new Vector2(408, 50));
            SocialTheme.Button(owner.loginButton);
            var buttonText = owner.loginButton.GetComponentInChildren<Text>();
            SocialTheme.Text(buttonText, SocialTheme.ButtonSize);
            buttonText.text = "登录";
            Place(buttonText.rectTransform, 0, new Vector2(380, 44));
            SocialTheme.Text(owner.statusText, SocialTheme.SmallSize);
            owner.statusText.color = SocialTheme.Muted;
            Place(owner.statusText.rectTransform, -204, new Vector2(408, 56));
            owner.statusText.text = "欢迎回来，请登录你的账号。";
        }
        public static Button AddRegisterEntry(LoginSceneController owner)
        {
            var button = SocialUi.Button(owner.usernameInput.transform.parent, "没有账号？注册", owner.ShowRegistration);
            button.name = "RegisterEntry";
            Place(button.GetComponent<RectTransform>(), -153, new Vector2(408, 32));
            button.GetComponent<Image>().color = SocialTheme.InputSurface;
            return button;
        }
        private static void ConfigureInput(InputField field, string hint, float y)
        {
            Place(field.GetComponent<RectTransform>(), y, new Vector2(408, 50));
            Place(field.textComponent.rectTransform, 0, new Vector2(376, 42));
            if (field.placeholder is Text placeholder)
            {
                placeholder.text = hint;
                Place(placeholder.rectTransform, 0, new Vector2(376, 42));
            }
            SocialTheme.Input(field);
        }
        private static void Caption(Transform panel, string name, string value, float y)
        {
            var existing = panel.Find(name);
            var text = existing != null ? existing.GetComponent<Text>() : SocialUi.Label(panel, value, 16);
            text.name = name;
            SocialTheme.Text(text, SocialTheme.SmallSize);
            text.color = SocialTheme.Muted;
            text.alignment = TextAnchor.MiddleLeft;
            Place(text.rectTransform, y, new Vector2(408, 28));
        }
        private static void Place(RectTransform rect, float y, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = size;
        }
    }
}
