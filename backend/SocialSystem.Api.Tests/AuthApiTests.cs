using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.Models.Entities;
using Xunit;

namespace SocialSystem.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);
    public string ConnectionString { get; } = GetTestConnection();

    private static string GetTestConnection()
    {
        var value = Environment.GetEnvironmentVariable("SOCIAL_TEST_MYSQL")
            ?? throw new InvalidOperationException("Run backend/scripts/Test-Auth.ps1 to start an isolated MySQL test instance.");
        var parsed = new MySqlConnectionStringBuilder(value);
        if (parsed.Database != "social_system_auth_tests" || parsed.Port == 3306 ||
            parsed.Server != "127.0.0.1")
            throw new InvalidOperationException("Only the isolated local test database is allowed.");
        return value;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:SocialSystem"] = ConnectionString,
                ["Jwt:SigningKey"] = Convert.ToBase64String(Key),
                ["Jwt:Issuer"] = "SocialSystem.Api",
                ["Jwt:Audience"] = "SocialSystem.Client",
                ["Jwt:ExpirationMinutes"] = "60"
            }));
    }
}

public sealed class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Name() => "test_" + Guid.NewGuid().ToString("N")[..20];
    private static string Password() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private static async Task<HttpResponseMessage> Register(HttpClient client, string name, string password) =>
        await client.PostAsJsonAsync("/api/auth/register", new { username = name, password, nickname = "测试用户 😀" });

    [Fact]
    public async Task Register_login_and_protected_endpoint_use_real_hash_and_safe_responses()
    {
        using var client = factory.CreateClient();
        var name = Name();
        var password = Password();
        using var registered = await Register(client, name.ToUpperInvariant(), password);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var registeredJson = await registered.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", registeredJson, StringComparison.OrdinalIgnoreCase);
        var user = (await registered.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(name, user.Username);
        Assert.True(user.Id > 0);

        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT password_hash FROM users WHERE id = @id";
        command.Parameters.AddWithValue("@id", user.Id);
        var hash = (string)(await command.ExecuteScalarAsync())!;
        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(new User(), hash, password));

        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username = name.ToUpperInvariant(), password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain("password", await login.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal("Bearer", token.TokenType);
        Assert.InRange(token.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(61));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(user.Id, (await me.Content.ReadFromJsonAsync<UserResponse>())!.Id);
    }

    [Fact]
    public async Task Passwords_receive_distinct_salts()
    {
        using var client = factory.CreateClient();
        var name1 = Name(); var name2 = Name(); var password = Password();
        using var first = await Register(client, name1, password);
        using var second = await Register(client, name2, password);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(DISTINCT password_hash) FROM users WHERE username IN (@a, @b)";
        command.Parameters.AddWithValue("@a", name1); command.Parameters.AddWithValue("@b", name2);
        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Concurrent_duplicate_registration_returns_one_created_and_one_conflict()
    {
        using var client = factory.CreateClient();
        var name = Name(); var password = Password();
        var responses = await Task.WhenAll(Register(client, name, password), Register(client, name.ToUpperInvariant(), password));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("username_taken", await conflict.Content.ReadAsStringAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("", "validPassword123", "昵称")]
    [InlineData("ab", "validPassword123", "昵称")]
    [InlineData("bad name", "validPassword123", "昵称")]
    [InlineData("用户姓名", "validPassword123", "昵称")]
    [InlineData("valid_name", "short", "昵称")]
    [InlineData("valid_name", "        ", "昵称")]
    [InlineData("valid_name", "validPassword123", " \t\n")]
    public async Task Invalid_registration_returns_400(string username, string password, string nickname)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { username, password, nickname });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlong_registration_fields_return_400()
    {
        using var client = factory.CreateClient();
        foreach (var input in new[]
        {
            new { username = new string('a', 33), password = Password(), nickname = "昵称" },
            new { username = Name(), password = new string('a', 129), nickname = "昵称" },
            new { username = Name(), password = Password(), nickname = new string('字', 33) }
        })
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", input);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Null_and_malformed_json_return_400()
    {
        using var client = factory.CreateClient();
        foreach (var body in new[] { "null", "{", "{\"username\":null,\"password\":null,\"nickname\":null}" })
        {
            using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/api/auth/register", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Unknown_user_and_wrong_password_have_same_generic_error()
    {
        using var client = factory.CreateClient();
        var name = Name();
        using var registered = await Register(client, name, Password());
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        foreach (var username in new[] { name, Name() })
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password() });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_credentials", body.GetProperty("code").GetString());
            Assert.Equal("用户名或密码错误", body.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task Missing_and_malformed_tokens_are_rejected()
    {
        using var client = factory.CreateClient();
        using var missing = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var malformed = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    public async Task Invalid_signed_tokens_are_rejected(string fault)
    {
        using var client = factory.CreateClient();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            fault == "issuer" ? "wrong" : "SocialSystem.Api",
            fault == "audience" ? "wrong" : "SocialSystem.Client",
            [new Claim("sub", "1")],
            now.AddHours(-2), fault == "expired" ? now.AddMinutes(-1) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(fault == "signature" ? RandomNumberGenerator.GetBytes(32) : factory.Key), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
