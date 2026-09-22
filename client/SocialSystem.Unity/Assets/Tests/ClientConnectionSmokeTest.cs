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
        finished = true;
        Debug.Log("PASS: Unity registration, LoginScene login, automatic Bearer GET, DELETE, logout and invalid-password handling.");
        EditorApplication.Exit(0);
    }
}
#endif
