using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialSystem.Api.DTOs.Messages;
using SocialSystem.Api.Services;

namespace SocialSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/messages")]
public sealed class MessagesController(MessageService messages) : ControllerBase
{
    private async Task<IActionResult> Execute(Func<long, Task<IActionResult>> action)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) || id <= 0)
            return Unauthorized();
        try { return await action(id); }
        catch (MessageOperationException error)
        {
            return Problem(statusCode: error.Status, title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }
    }

    [HttpPost("send")]
    public Task<IActionResult> Send(SendMessageRequest input, CancellationToken ct) =>
        Execute(async id => StatusCode(201, await messages.SendAsync(id, input, ct)));

    [HttpGet("{friendId:long}")]
    public Task<IActionResult> History([Range(1, long.MaxValue)] long friendId, CancellationToken ct) =>
        Execute(async id => Ok(await messages.HistoryAsync(id, friendId, ct)));
}

