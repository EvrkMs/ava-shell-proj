using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Auth.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _repo;
    private readonly AppDbContext _db;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictTokenManager _tokenManager;

    public SessionService(
        ISessionRepository repo,
        AppDbContext db,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager)
    {
        _repo = repo;
        _db = db;
        _authorizationManager = authorizationManager;
        _tokenManager = tokenManager;
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

        // Cascade revoke: if authorization id is known, use OpenIddict managers
        if (!string.IsNullOrWhiteSpace(s.AuthorizationId))
        {
            await RevokeByAuthorizationIdAsync(s.AuthorizationId!, ct);
        }
        else
        {
            // Fallback: match tokens by sid in payload (works for self-contained tokens)
            await RevokeOpenIddictTokensBySidAsync(sid, ct);
        }

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

    private async Task<int> RevokeOpenIddictTokensBySidAsync(string sid, CancellationToken ct)
    {
        // Use a parameterized raw SQL update to avoid coupling to OpenIddict EF models.
        // Note: access tokens are JWTs (not stored). They are enforced via middleware on each request.
        var revoked = OpenIddictConstants.Statuses.Revoked;
        var refreshType = "refresh_token";
        var accessType = "access_token";
        var like = $@"%""sid"":""{sid}""%";

        // Revoke refresh tokens containing this sid in their payload
        var sql = @"UPDATE ""OpenIddictTokens"" SET ""Status"" = {0}
                    WHERE (""Status"" IS NULL OR ""Status"" <> {0})
                      AND ""Type"" = {1}
                      AND ""Payload"" IS NOT NULL
                      AND ""Payload"" LIKE {2}";

        var count = 0;
        count += await _db.Database.ExecuteSqlRawAsync(sql, new object?[] { revoked, refreshType, like }, ct);
        // Also attempt to revoke access tokens if reference access tokens are enabled.
        count += await _db.Database.ExecuteSqlRawAsync(sql, new object?[] { revoked, accessType, like }, ct);
        return count;
    }

    private async Task RevokeByAuthorizationIdAsync(string authorizationId, CancellationToken ct)
    {
        var authorization = await _authorizationManager.FindByIdAsync(authorizationId, ct);
        if (authorization is not null)
        {
            try { await _authorizationManager.TryRevokeAsync(authorization!, ct); } catch { }
        }

        await foreach (var token in _tokenManager.FindByAuthorizationIdAsync(authorizationId, ct))
        {
            try { await _tokenManager.TryRevokeAsync(token!, ct); } catch { }
        }
    }

    public async Task<bool> LinkAuthorizationAsync(string sid, string authorizationId, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(sid, "N", out var id)) return false;
        var s = await _repo.GetAsync(id, ct);
        if (s is null) return false;
        if (!string.IsNullOrEmpty(s.AuthorizationId)) return true; // already linked
        s.AuthorizationId = authorizationId;
        await _repo.UpdateAsync(s, ct);
        return true;
    }
}
