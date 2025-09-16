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
    private readonly ISessionService _sessions;

    public SessionsController(ISessionRepository repo, ISessionService sessions)
    {
        _repo = repo;
        _sessions = sessions;
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
    [Authorize(Policy = "ApiRead")]
    public async Task<ActionResult<IEnumerable<UserSession>>> List([FromQuery] bool all = false, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = new List<UserSession>();
        await foreach (var s in _repo.ListByUserAsync(userId, onlyActive: !all, ct: ct))
            result.Add(s);
        return Ok(result);
    }

    [HttpGet("current")]
    [Authorize(Policy = "ApiRead")]
    public async Task<ActionResult<UserSession>> Current(CancellationToken ct = default)
    {
        var sid = User.FindFirstValue("sid");
        if (string.IsNullOrWhiteSpace(sid) || !Guid.TryParseExact(sid, "N", out var id))
            return NotFound(new { error = "no_sid" });
        var s = await _repo.GetAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost("revoke-all")]
    [Authorize(Policy = "ApiWrite")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        // Do not revoke the session from which this action is performed
        var currentSid = User.FindFirstValue("sid");
        var count = 0;
        await foreach (var s in _repo.ListByUserAsync(userId, onlyActive: true, ct: ct))
        {
            var sid = s.Id.ToString("N");
            if (!string.IsNullOrWhiteSpace(currentSid) && string.Equals(sid, currentSid, StringComparison.OrdinalIgnoreCase))
                continue;
            if (await _sessions.RevokeAsync(sid, reason: "user_revoked_all", by: userId.ToString(), ct))
                count++;
        }
        return Ok(new { revoked = count });
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = "ApiWrite")]
    public async Task<IActionResult> RevokeOne(Guid id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var s = await _repo.GetAsync(id, ct);
        if (s is null) return NotFound();
        if (s.UserId != userId) return Forbid();
        if (!await _sessions.RevokeAsync(id.ToString("N"), reason: "user_revoked", by: userId.ToString(), ct))
            return NoContent();
        return NoContent();
    }
}
