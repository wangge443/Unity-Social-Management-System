using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialSystem.Api.DTOs.Posts;
using SocialSystem.Api.Services;

namespace SocialSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public sealed class PostsController(PostService posts) : ControllerBase
{
    private async Task<IActionResult> Execute(Func<long, Task<IActionResult>> action)
    {
        if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) || id <= 0)
            return Unauthorized();
        try { return await action(id); }
        catch (PostOperationException error)
        {
            return Problem(statusCode: error.Status, title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }
    }

    [HttpPost]
    public Task<IActionResult> Create(CreatePostRequest input, CancellationToken ct) =>
        Execute(async userId => StatusCode(201, await posts.CreateAsync(userId, input, ct)));

    [HttpGet]
    public Task<IActionResult> List([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        Execute(async userId => Ok(await posts.ListAsync(userId, page, pageSize, ct)));

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete([Range(1, long.MaxValue)] long id, CancellationToken ct) =>
        Execute(async userId => { await posts.DeleteAsync(userId, id, ct); return NoContent(); });

    [HttpGet("{id:long}/comments")]
    public Task<IActionResult> Comments([Range(1, long.MaxValue)] long id,
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        Execute(async userId => Ok(await posts.CommentsAsync(userId, id, page, pageSize, ct)));


    [HttpPost("{id:long}/comments")]
    public Task<IActionResult> Comment([Range(1, long.MaxValue)] long id, CreateCommentRequest input, CancellationToken ct) =>
        Execute(async userId => StatusCode(201, await posts.CommentAsync(userId, id, input, ct)));

    [HttpPost("{id:long}/like")]
    public Task<IActionResult> Like([Range(1, long.MaxValue)] long id, CancellationToken ct) =>
        Execute(async userId => { await posts.SetLikeAsync(userId, id, true, ct); return NoContent(); });

    [HttpDelete("{id:long}/like")]
    public Task<IActionResult> Unlike([Range(1, long.MaxValue)] long id, CancellationToken ct) =>
        Execute(async userId => { await posts.SetLikeAsync(userId, id, false, ct); return NoContent(); });
}
