using System.Security.Claims;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace Auth.Host.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _repo;

    public SessionsController(ISessionRepository repo)
    {
        _repo = repo;
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = default;
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return Guid.TryParse(sub, out userId);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserSession>>> List([FromQuery] bool all = false, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = new List<UserSession>();
        await foreach (var s in _repo.ListByUserAsync(userId, onlyActive: !all, ct: ct))
            result.Add(s);
        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<ActionResult<UserSession>> Current(CancellationToken ct = default)
    {
        var sid = User.FindFirstValue("sid");
        if (string.IsNullOrWhiteSpace(sid) || !Guid.TryParseExact(sid, "N", out var id))
            return NotFound(new { error = "no_sid" });
        var s = await _repo.GetAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var now = DateTime.UtcNow;
        var count = 0;
        await foreach (var s in _repo.ListByUserAsync(userId, onlyActive: true, ct: ct))
        {
            s.Revoked = true;
            s.RevokedAt = now;
            s.RevokedBy = userId.ToString();
            await _repo.UpdateAsync(s, ct);
            count++;
        }
        return Ok(new { revoked = count });
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> RevokeOne(Guid id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var s = await _repo.GetAsync(id, ct);
        if (s is null) return NotFound();
        if (s.UserId != userId) return Forbid();
        if (s.Revoked) return NoContent();
        s.Revoked = true;
        s.RevokedAt = DateTime.UtcNow;
        s.RevokedBy = userId.ToString();
        await _repo.UpdateAsync(s, ct);
        return NoContent();
    }
}

