namespace Auth.Application.Interfaces;

public interface ISessionService
{
    Task<string> EnsureInteractiveSessionAsync(Guid userId, string? clientId, string? ip, string? userAgent, string? device, TimeSpan? absoluteLifetime, CancellationToken ct = default);
    Task<bool> TouchAsync(string sid, CancellationToken ct = default);
    Task<bool> RevokeAsync(string sid, string? reason = null, string? by = null, CancellationToken ct = default);
    Task<bool> IsActiveAsync(string sid, CancellationToken ct = default);
    Task<bool> LinkAuthorizationAsync(string sid, string authorizationId, CancellationToken ct = default);
}
