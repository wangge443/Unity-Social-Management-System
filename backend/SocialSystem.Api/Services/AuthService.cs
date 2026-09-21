using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs.Auth;
using SocialSystem.Api.Models.Entities;
namespace SocialSystem.Api.Services;

public sealed class DuplicateUsernameException : Exception { }

public sealed class AuthService(SocialDbContext db, IPasswordHasher<User> hasher, TokenService tokens)
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Username == username, ct))
            throw new DuplicateUsernameException();
        var user = new User { Username = username, Nickname = request.Nickname.Trim() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            // The database unique key handles concurrent registration, too.
            db.Entry(user).State = EntityState.Detached;
            throw new DuplicateUsernameException();
        }
        return UserResponse.From(user);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username, ct);
        if (user is null) return null;
        PasswordVerificationResult result;
        try { result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password); }
        catch (FormatException) { return null; }
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }
        return tokens.Issue(user);
    }
}

