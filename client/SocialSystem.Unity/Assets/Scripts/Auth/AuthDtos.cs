using System;
namespace SocialSystem.Client.Auth
{
    [Serializable] public sealed class LoginRequest { public string username; public string password; }
    [Serializable] public sealed class RegisterRequest { public string username; public string password; public string nickname; }
    [Serializable] public sealed class UserResponse { public long id; public string username; public string nickname; }
    [Serializable] public sealed class LoginResponse
    {
        public string accessToken;
        public string tokenType;
        public string expiresAtUtc;
        public UserResponse user;
    }
}
