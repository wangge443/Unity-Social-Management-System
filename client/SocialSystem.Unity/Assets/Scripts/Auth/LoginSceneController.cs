using System.Collections;
using SocialSystem.Client.Network;
using UnityEngine;
using UnityEngine.UI;
namespace SocialSystem.Client.Auth
{
    public sealed class LoginSceneController : MonoBehaviour
    {
        public AuthManager authManager;
        public InputField usernameInput;
        public InputField passwordInput;
        public Button loginButton;
        public Text statusText;
        public SocialSystem.Client.Main.SocialAppController App { get; private set; }
        public bool IsBusy { get; private set; }
        public bool LoginVerified { get; private set; }

        private void Awake()
        {
            LoginSceneView.Apply(this);
            loginButton.onClick.AddListener(Login);
            App = gameObject.AddComponent<SocialSystem.Client.Main.SocialAppController>();
            App.Initialize(this);
        }
        private void OnDestroy() { loginButton.onClick.RemoveListener(Login); }
        public void Login()
        {
            if (IsBusy) return;
            LoginVerified = false;
            if (string.IsNullOrWhiteSpace(usernameInput.text) || string.IsNullOrWhiteSpace(passwordInput.text))
            {
                statusText.text = "请输入用户名和密码。";
                return;
            }
            StartCoroutine(LoginFlow());
        }
        private IEnumerator LoginFlow()
        {
            SetBusy(true);
            statusText.text = "正在登录，请稍候…";
            ApiResponse<LoginResponse> login = null;
            yield return authManager.Login(usernameInput.text.Trim(), passwordInput.text, response => login = response);
            passwordInput.text = "";
            if (login == null || !login.Success)
            {
                statusText.text = login != null && !login.Success && login.StatusCode == 200 ? "登录响应无效，请重试。" : login?.Error ?? "登录未完成，请重试。";
                SetBusy(false);
                yield break;
            }
            statusText.text = "正在确认登录状态…";
            ApiResponse<UserResponse> current = null;
            yield return authManager.GetCurrentUser(response => current = response);
            LoginVerified = current != null && current.Success && current.Data != null &&
                current.Data.id == login.Data.user.id;
            statusText.text = LoginVerified
                ? "登录成功，欢迎 " + current.Data.nickname + "。"
                : current?.Error ?? "无法确认登录状态，请重试。";
            if (!LoginVerified) authManager.Logout();
            SetBusy(false);
            if (LoginVerified) App.Open();
        }
        public void ResetLogin() { LoginVerified = false; passwordInput.text = ""; }
        private void SetBusy(bool value)
        {
            IsBusy = value;
            loginButton.interactable = !value;
            usernameInput.interactable = !value;
            passwordInput.interactable = !value;
        }
    }
}
