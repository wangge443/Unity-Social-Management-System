using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
namespace SocialSystem.Api.Middleware;
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var databaseError = exception is MySqlException or DbUpdateException;
        var status = databaseError ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        logger.LogError("Request failed: {ExceptionType}", exception.GetType().Name);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status, Title = databaseError ? "数据库暂时不可用，请稍后重试" : "服务器内部错误",
                Extensions = { ["code"] = databaseError ? "database_unavailable" : "internal_error" }
            }
        });
    }
}

