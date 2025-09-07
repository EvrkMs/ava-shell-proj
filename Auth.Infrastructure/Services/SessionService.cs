using System.Net;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;

namespace Auth.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _repo;

    public SessionService(ISessionRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> EnsureInteractiveSessionAsync(Guid userId, string? clientId, string? ip, string? userAgent, string? device, TimeSpan? absoluteLifetime, CancellationToken ct = default)
    {
        var session = new UserSession
        {
            UserId = userId,
            ClientId = clientId,
            Device = device,
            UserAgent = Trunc(userAgent, 500),
            IpAddress = Trunc(ip, 100),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            ExpiresAt = absoluteLifetime.HasValue ? DateTime.UtcNow.Add(absoluteLifetime.Value) : null,
            Revoked = false
        };
        var created = await _repo.AddAsync(session, ct);
        return created.Id.ToString("N");
    }

    public async Task<bool> TouchAsync(string sid, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(sid, "N", out var id)) return false;
        var s = await _repo.GetActiveAsync(id, ct);
        if (s is null) return false;
        s.LastSeenAt = DateTime.UtcNow;
        await _repo.UpdateAsync(s, ct);
        return true;
    }

    public async Task<bool> RevokeAsync(string sid, string? reason = null, string? by = null, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(sid, "N", out var id)) return false;
        var s = await _repo.GetAsync(id, ct);
        if (s is null || s.Revoked) return false;
        s.Revoked = true;
        s.RevokedAt = DateTime.UtcNow;
        s.RevokedBy = by;
        s.RevocationReason = reason;
        await _repo.UpdateAsync(s, ct);
        return true;
    }

    public async Task<bool> IsActiveAsync(string sid, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(sid, "N", out var id)) return false;
        var s = await _repo.GetActiveAsync(id, ct);
        return s is not null;
    }

    private static string? Trunc(string? val, int max)
        => string.IsNullOrEmpty(val) ? val : (val!.Length <= max ? val : val.Substring(0, max));
}

