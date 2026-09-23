#if UNITY_EDITOR
using System;
using System.Collections;
using SocialSystem.Client.Auth;
using SocialSystem.Client.Network;
using UnityEditor;
using UnityEngine;
public sealed class ClientConnectionSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class PostBody { public string content = "Unity network test"; }
    [Serializable] private sealed class PostResult { public long id = 0; }
    private bool finished;
    private void Fail(string message)
    {
        finished = true;
        Debug.LogError("Client smoke test failed: " + message);
        EditorApplication.Exit(1);
    }
    private IEnumerator Timeout()
    {
        yield return new WaitForSecondsRealtime(90);
        if (!finished) Fail("Timeout");
    }
    private IEnumerator Start()
    {
        StartCoroutine(Timeout());
        var ui = FindAnyObjectByType<LoginSceneController>();
        var username = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_USERNAME");
        var password = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_PASSWORD");
        var url = Environment.GetEnvironmentVariable("SOCIAL_UNITY_TEST_URL");
        if (ui == null || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(url))
        { Fail("Missing test configuration."); yield break; }
        var auth = ui.authManager;
        auth.apiClient.baseUrl = url;
        ApiResponse<UserResponse> registration = null;
        yield return auth.Register(username, password, "Unity test", r => registration = r);
        if (registration == null || !registration.Success) { Fail("Registration"); yield break; }
        ui.usernameInput.text = username;
        ui.passwordInput.text = password;
        ui.loginButton.onClick.Invoke();
        while (ui.IsBusy) yield return null;
        if (!ui.LoginVerified || !auth.tokenManager.HasToken || auth.CurrentUser.id != registration.Data.id)
        { Fail("LoginScene login or Bearer GET"); yield break; }
        ApiResponse<PostResult> post = null;
        yield return auth.apiClient.Post<PostResult>("/api/posts", new PostBody(), r => post = r);
        if (post == null || !post.Success || post.Data == null || post.Data.id <= 0)
        { Fail("Authenticated POST"); yield break; }
        ApiResponse<string> deletion = null;
        yield return auth.apiClient.Delete<string>("/api/posts/" + post.Data.id, r => deletion = r);
        if (deletion == null || !deletion.Success || deletion.StatusCode != 204 || deletion.Data != null)
        { Fail("Authenticated DELETE with empty response"); yield break; }
        auth.Logout();
        ApiResponse<UserResponse> unauthorized = null;
        yield return auth.GetCurrentUser(r => unauthorized = r);
        if (unauthorized == null || unauthorized.StatusCode != 401) { Fail("Logout"); yield break; }
        ui.usernameInput.text = username;
        ui.passwordInput.text = Guid.NewGuid().ToString("N");
        ui.loginButton.onClick.Invoke();
        while (ui.IsBusy) yield return null;
        if (ui.LoginVerified || auth.tokenManager.HasToken) { Fail("Wrong-password handling"); yield break; }
        // Exercise the actual registration UI against the isolated API and MySQL.
        yield return null;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "register-login");
        ui.RegisterEntry.onClick.Invoke();
        var form = ui.Registration;
        if (!form.IsVisible || ui.usernameInput.transform.parent.gameObject.activeSelf)
        { Fail("Registration navigation"); yield break; }
        form.Username.text = "bad name";
        form.Nickname.text = "注册测试";
        form.Password.text = password;
        form.ConfirmPassword.text = password;
        form.Submit.onClick.Invoke();
        if (ui.IsBusy || !form.Status.text.Contains("用户名"))
        { Fail("Registration username validation"); yield break; }
        form.Username.text = username;
        form.Nickname.text = " ";
        form.Submit.onClick.Invoke();
        if (ui.IsBusy || !form.Status.text.Contains("昵称"))
        { Fail("Registration nickname validation"); yield break; }
        form.Nickname.text = "注册测试";
        form.Password.text = form.ConfirmPassword.text = "short";
        form.Submit.onClick.Invoke();
        if (ui.IsBusy || !form.Status.text.Contains("密码"))
        { Fail("Registration password length"); yield break; }
        form.Password.text = password;
        form.ConfirmPassword.text = password + "x";
        form.Submit.onClick.Invoke();
        if (ui.IsBusy || !form.Status.text.Contains("不一致"))
        { Fail("Registration confirmation"); yield break; }
        form.ConfirmPassword.text = password;
        form.Submit.onClick.Invoke();
        while (ui.IsBusy) yield return null;
        if (!form.IsVisible || !form.Status.text.Contains("已被注册") || auth.tokenManager.HasToken)
        { Fail("Registration duplicate username"); yield break; }
        auth.apiClient.baseUrl = "http://127.0.0.1:1";
        form.Submit.onClick.Invoke();
        while (ui.IsBusy) yield return null;
        if (!form.IsVisible || !form.Status.text.Contains("无法确认") || !form.Submit.IsInteractable())
        { Fail("Registration offline recovery"); yield break; }
        auth.apiClient.baseUrl = url;
        form.Back.onClick.Invoke();
        if (form.IsVisible || !ui.usernameInput.transform.parent.gameObject.activeSelf ||
            form.Password.text != "" || form.ConfirmPassword.text != "")
        { Fail("Registration return and password clearing"); yield break; }
        ui.RegisterEntry.onClick.Invoke();
        form.Username.text = username + "_r";
        form.Nickname.text = "注册测试";
        form.Password.text = form.ConfirmPassword.text = password;
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "register-form");
        if (form.Password.contentType != UnityEngine.UI.InputField.ContentType.Password ||
            form.ConfirmPassword.contentType != UnityEngine.UI.InputField.ContentType.Password)
        { Fail("Registration password masks"); yield break; }
        form.Submit.onClick.Invoke();
        if (!ui.IsBusy || form.Submit.IsInteractable() || form.Back.IsInteractable() || ui.RegisterEntry.interactable)
        { Fail("Registration duplicate-submit guard"); yield break; }
        while (ui.IsBusy) yield return null;
        if (!ui.LoginVerified || !auth.tokenManager.HasToken || !ui.App.IsOpen || form.IsVisible ||
            auth.CurrentUser.username != username + "_r" || auth.CurrentUser.nickname != "注册测试" ||
            auth.CurrentUser.id == registration.Data.id || form.Password.text != "" || form.ConfirmPassword.text != "")
        { Fail("Registration automatic login, me verification and HomeView"); yield break; }
        ClientUiCapture.Save(ui.GetComponent<Canvas>(), "register-home");
        ui.App.Logout();
        yield return null;
        ui.usernameInput.text = username + "_r";
        ui.passwordInput.text = password;
        ui.loginButton.onClick.Invoke();
        while (ui.IsBusy) yield return null;
        if (!ui.LoginVerified || !ui.App.IsOpen || auth.CurrentUser.username != username + "_r")
        { Fail("Registered account subsequent normal login"); yield break; }
        Debug.Log("PASS: Unity registration UI validation, duplicate username, offline recovery, navigation, automatic JWT login, me verification, HomeView and subsequent login.");

        finished = true;
        Debug.Log("PASS: Unity registration, LoginScene login, automatic Bearer GET, DELETE, logout and invalid-password handling.");
        EditorApplication.Exit(0);
    }
}
#endif
