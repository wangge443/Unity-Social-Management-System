using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialSystem.Api.Data;
using SocialSystem.Api.DTOs;

namespace SocialSystem.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IWebHostEnvironment environment, IConfiguration configuration,
    IServiceProvider services, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("database")]
    public async Task<ActionResult<DatabaseCheckResponse>> Database(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("SocialSystem")))
            return Problem(statusCode: 503, title: "Database configuration missing",
                detail: "Set ConnectionStrings:SocialSystem in local User Secrets.");

        try
        {
            var db = services.GetRequiredService<SocialDbContext>();
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT DATABASE()";
                var database = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
                if (database != "social_system")
                    return Problem(statusCode: 503, title: "Unexpected database",
                        detail: "The configured database must be social_system.");

                // Select mapped columns even for empty tables; never return row contents.
                await db.Users.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.FriendRequests.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.Friends.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.Messages.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.Posts.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.Comments.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                await db.Likes.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                return Ok(new DatabaseCheckResponse("ok", database, 7));
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Do not log exception messages: they may include connection information.
            logger.LogWarning("Database check failed ({ExceptionType}).", exception.GetType().Name);
            return Problem(statusCode: 503, title: "Database check failed",
                detail: "Check local credentials, MySQL availability and the existing schema.");
        }
    }
}

