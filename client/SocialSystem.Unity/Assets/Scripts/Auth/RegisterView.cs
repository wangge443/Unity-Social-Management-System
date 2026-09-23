using SocialSystem.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.Auth
{
    // Runtime presentation only; reuses the existing scene, theme and authentication services.
    public sealed class RegisterView
    {
        private readonly RectTransform panel;
        private readonly CanvasGroup controls;
        public InputField Username { get; }
        public InputField Nickname { get; }
        public InputField Password { get; }
        public InputField ConfirmPassword { get; }
        public Button Submit { get; }
        public Button Back { get; }
        public Text Status { get; }
        public bool IsVisible => panel.gameObject.activeSelf;

        public RegisterView(LoginSceneController owner)
        {
            panel = SocialUi.Rect("RegisterPanel", owner.usernameInput.transform.parent.parent);
            Place(panel, 0, 0, 480, 490);
            panel.gameObject.AddComponent<Image>().color = SocialTheme.Surface;
            controls = panel.gameObject.AddComponent<CanvasGroup>();
            var title = SocialUi.Label(panel, "创建账号", 30);
            title.alignment = TextAnchor.MiddleCenter;
            Place(title.rectTransform, 0, 203, 408, 40);
            var subtitle = SocialUi.Label(panel, "注册成功后自动登录，开启校园社交", 16);
            subtitle.color = SocialTheme.Muted;
            subtitle.alignment = TextAnchor.MiddleCenter;
            Place(subtitle.rectTransform, 0, 165, 408, 28);
            Username = Field("用户名", "3–32 位字母、数字或下划线", 32, 116);
            Nickname = Field("昵称", "请输入昵称（最多 32 字符）", 32, 62);
            Password = Field("密码", "8–128 字符", 128, 8);
            ConfirmPassword = Field("确认密码", "请再次输入密码", 128, -46);
            Password.contentType = ConfirmPassword.contentType = InputField.ContentType.Password;
            Submit = SocialUi.Button(panel, "注册", owner.Register);
            Place(Submit.GetComponent<RectTransform>(), 0, -108, 408, 44);
            Back = SocialUi.Button(panel, "返回登录", owner.ShowLogin);
            Place(Back.GetComponent<RectTransform>(), 0, -158, 408, 32);
            Back.GetComponent<Image>().color = SocialTheme.InputSurface;
            Status = SocialUi.Label(panel, "", 16);
            Status.color = SocialTheme.Muted;
            Status.alignment = TextAnchor.MiddleCenter;
            Place(Status.rectTransform, 0, -211, 408, 56);
            panel.gameObject.SetActive(false);
        }
        private InputField Field(string label, string hint, int limit, float y)
        {
            var caption = SocialUi.Label(panel, label, 16);
            caption.color = SocialTheme.Muted;
            Place(caption.rectTransform, -160, y, 88, 40);
            var field = SocialUi.Input(panel, hint, limit);
            field.name = "Register" + label;
            Place(field.GetComponent<RectTransform>(), 52, y, 304, 40);
            return field;
        }
        public void Show(string username)
        {
            Username.text = username;
            ClearPasswords();
            Status.text = "请填写以下信息，密码不会保存到本地。";
            panel.gameObject.SetActive(true);
            Username.ActivateInputField();
        }
        public void Hide()
        {
            ClearPasswords();
            panel.gameObject.SetActive(false);
        }
        public void ClearPasswords() { Password.text = ""; ConfirmPassword.text = ""; }
        public void SetBusy(bool busy) { controls.interactable = !busy; }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
