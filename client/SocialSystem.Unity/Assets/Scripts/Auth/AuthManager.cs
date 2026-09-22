using System;
using System.Collections;
using System.Globalization;
using SocialSystem.Client.Network;
using UnityEngine;
namespace SocialSystem.Client.Auth
{
    public sealed class AuthManager : MonoBehaviour
    {
        public ApiClient apiClient;
        public TokenManager tokenManager;
        public UserResponse CurrentUser { get; private set; }

        public IEnumerator Login(string username, string password, Action<ApiResponse<LoginResponse>> completed)
        {
            Logout();
            yield return apiClient.Post<LoginResponse>("/api/auth/login",
                new LoginRequest { username = username, password = password }, response =>
                {
                    if (response.Success)
                    {
                        var data = response.Data;
                        if (data == null || data.user == null || string.IsNullOrWhiteSpace(data.accessToken) ||
                            !string.Equals(data.tokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                            !DateTimeOffset.TryParse(data.expiresAtUtc, CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal, out var expiration) || expiration <= DateTimeOffset.UtcNow)
                        {
                            completed?.Invoke(ApiResponse<LoginResponse>.Failure("Invalid login response.", response.StatusCode));
                            return;
                        }
                        tokenManager.Save(data.accessToken, expiration);
                        CurrentUser = data.user;
                    }
                    completed?.Invoke(response);
                }, authorize: false);
        }
        public IEnumerator Register(string username, string password, string nickname, Action<ApiResponse<UserResponse>> completed) =>
            apiClient.Post<UserResponse>("/api/auth/register",
                new RegisterRequest { username = username, password = password, nickname = nickname }, completed, authorize: false);
        public IEnumerator GetCurrentUser(Action<ApiResponse<UserResponse>> completed) =>
            apiClient.Get<UserResponse>("/api/auth/me", response =>
            {
                CurrentUser = response.Success ? response.Data : null;
                completed?.Invoke(response);
            });
        public void Logout() { tokenManager.Clear(); CurrentUser = null; }
    }
}
