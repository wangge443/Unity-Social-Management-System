using System.Collections;
using System.Text.RegularExpressions;
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
        public Button RegisterEntry { get; private set; }
        public RegisterView Registration { get; private set; }
        public SocialSystem.Client.Main.SocialAppController App { get; private set; }
        public bool IsBusy { get; private set; }
        public bool LoginVerified { get; private set; }

        private void Awake()
        {
            LoginSceneView.Apply(this);
            loginButton.onClick.AddListener(Login);
            RegisterEntry = LoginSceneView.AddRegisterEntry(this);
            Registration = new RegisterView(this);
            App = gameObject.AddComponent<SocialSystem.Client.Main.SocialAppController>();
            App.Initialize(this);
        }
        private void OnDestroy() { loginButton.onClick.RemoveListener(Login); }
        public void ShowRegistration()
        {
            if (IsBusy || App.IsOpen) return;
            LoginVerified = false;
            passwordInput.text = "";
            usernameInput.transform.parent.gameObject.SetActive(false);
            Registration.Show(usernameInput.text.Trim());
        }
        public void ShowLogin()
        {
            if (IsBusy || App.IsOpen) return;
            usernameInput.text = Registration.Username.text.Trim();
            Registration.Hide();
            usernameInput.transform.parent.gameObject.SetActive(true);
            passwordInput.text = "";
            statusText.text = "欢迎回来，请登录你的账号。";
        }
        public void Register()
        {
            if (IsBusy || !Registration.IsVisible || App.IsOpen) return;
            var username = Registration.Username.text.Trim();
            var nickname = Registration.Nickname.text.Trim();
            var password = Registration.Password.text;
            string error = null;
            if (!Regex.IsMatch(username, @"\A[A-Za-z0-9_]{3,32}\z"))
                error = "用户名须为 3–32 位字母、数字或下划线。";
            else if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > 32)
                error = "请输入昵称，长度不能超过 32 字符。";
            else if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
                error = "密码须为 8–128 字符，不能全为空白。";
            else if (password != Registration.ConfirmPassword.text)
                error = "两次输入的密码不一致，请重新确认。";
            if (error != null) { Registration.Status.text = error; return; }
            StartCoroutine(RegisterFlow(username, password, nickname));
        }
        private IEnumerator RegisterFlow(string username, string password, string nickname)
        {
            SetBusy(true);
            LoginVerified = false;
            Registration.Status.text = "正在注册，请稍候…";
            ApiResponse<UserResponse> registration = null;
            yield return authManager.Register(username, password, nickname, response => registration = response);
            if (registration == null || !registration.Success || registration.Data == null || registration.Data.id <= 0)
            {
                Registration.Status.text = RegistrationError(registration);
                SetBusy(false);
                yield break;
            }
            Registration.Hide();
            usernameInput.text = username;
            passwordInput.text = "";
            usernameInput.transform.parent.gameObject.SetActive(true);
            yield return LoginFlow(username, password, true);
        }
        private static string RegistrationError(ApiResponse<UserResponse> response)
        {
            if (response == null || response.StatusCode == 0)
                return "无法确认注册结果，请检查网络；若已提交，可先返回登录尝试。";
            if (response.Success) return "注册响应无效，无法自动登录；请返回登录尝试。";
            switch (response.StatusCode)
            {
                case 400: return "注册信息无效，请检查用户名、昵称及密码长度。";
                case 409: return "该用户名已被注册，请更换用户名或返回登录。";
                case 500:
                case 503: return "服务器暂时无法完成注册，请稍后重试。";
                default: return "注册失败（HTTP " + response.StatusCode + "），请稍后重试。";
            }
        }
        public void Login()
        {
            if (IsBusy || Registration.IsVisible || App.IsOpen) return;
            LoginVerified = false;
            if (string.IsNullOrWhiteSpace(usernameInput.text) || string.IsNullOrWhiteSpace(passwordInput.text))
            {
                statusText.text = "请输入用户名和密码。";
                return;
            }
            StartCoroutine(LoginFlow(usernameInput.text.Trim(), passwordInput.text));
        }
        private IEnumerator LoginFlow(string username, string password, bool afterRegistration = false)
        {
            SetBusy(true);
            statusText.text = afterRegistration ? "注册成功，正在自动登录…" : "正在登录，请稍候…";
            ApiResponse<LoginResponse> login = null;
            yield return authManager.Login(username, password, response => login = response);
            passwordInput.text = "";
            if (login == null || !login.Success)
            {
                statusText.text = login != null && !login.Success && login.StatusCode == 200 ? "登录响应无效，请重试。" : login?.Error ?? "登录未完成，请重试。";
                if (afterRegistration) statusText.text = "账号已注册，自动登录未完成，请输入密码登录。";
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
            if (!LoginVerified)
            {
                authManager.Logout();
                if (afterRegistration) statusText.text = "账号已注册，登录验证未完成，请重新登录。";
            }
            SetBusy(false);
            if (LoginVerified) App.Open();
        }
        public void ResetLogin() { LoginVerified = false; passwordInput.text = ""; Registration.Hide(); }
        private void SetBusy(bool value)
        {
            IsBusy = value;
            loginButton.interactable = !value;
            usernameInput.interactable = !value;
            passwordInput.interactable = !value;
            RegisterEntry.interactable = !value;
            Registration.SetBusy(value);
        }
    }
}
