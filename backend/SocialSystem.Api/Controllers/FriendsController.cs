using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialSystem.Api.DTOs.Friends;
using SocialSystem.Api.Services;

namespace SocialSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/friends")]
public sealed class FriendsController(FriendService friends) : ControllerBase
{
    private async Task<IActionResult> Execute(Func<long, Task<IActionResult>> action)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) || id <= 0)
            return Unauthorized();
        try { return await action(id); }
        catch (FriendOperationException error)
        {
            return Problem(statusCode: error.Status, title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }
    }

    [HttpPost("request")]
    public Task<IActionResult> SendRequest(SendFriendRequest request, CancellationToken ct) =>
        Execute(async id => StatusCode(201, await friends.SendAsync(id, request.ReceiverId, ct)));

    [HttpPost("accept/{requestId:long}")]
    public Task<IActionResult> Accept([Range(1, long.MaxValue)] long requestId, CancellationToken ct) =>
        Execute(async id => Ok(await friends.AcceptAsync(id, requestId, ct)));

    [HttpDelete("{friendId:long}")]
    public Task<IActionResult> Delete([Range(1, long.MaxValue)] long friendId, CancellationToken ct) =>
        Execute(async id => { await friends.DeleteAsync(id, friendId, ct); return NoContent(); });

    [HttpGet]
    public Task<IActionResult> List([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        Execute(async id => Ok(await friends.ListAsync(id, page, pageSize, ct)));
}
