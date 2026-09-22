using System;
using UnityEngine;
namespace SocialSystem.Client.Auth
{
    public sealed class TokenManager : MonoBehaviour
    {
        private string token;
        private DateTimeOffset expiresAt;
        public bool HasToken => !string.IsNullOrEmpty(AccessToken);
        public string AccessToken
        {
            get
            {
                if (!string.IsNullOrEmpty(token) && DateTimeOffset.UtcNow >= expiresAt) Clear();
                return token;
            }
        }
        public void Save(string accessToken, DateTimeOffset expiration)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || expiration <= DateTimeOffset.UtcNow)
                throw new ArgumentException("A non-expired token is required.");
            token = accessToken;
            expiresAt = expiration;
        }
        public void Clear() { token = null; expiresAt = default; }
        private void OnDestroy() => Clear();
    }
}
