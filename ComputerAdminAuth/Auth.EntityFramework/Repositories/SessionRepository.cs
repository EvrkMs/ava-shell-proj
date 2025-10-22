using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.EntityFramework.Repositories;

public class SessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task<UserSession?> GetAsync(Guid id, CancellationToken ct = default)
        => await db.UserSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<UserSession?> GetByReferenceAsync(string referenceId, CancellationToken ct = default)
        => await db.UserSessions.FirstOrDefaultAsync(s => s.ReferenceId == referenceId, ct);

    public async Task<UserSession?> GetActiveByReferenceAsync(string referenceId, CancellationToken ct = default)
        => await db.UserSessions.FirstOrDefaultAsync(s =>
            s.ReferenceId == referenceId &&
            !s.Revoked &&
            (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow),
            ct);

    public async Task<UserSession> AddAsync(UserSession session, CancellationToken ct = default)
    {
        var entry = await db.UserSessions.AddAsync(session, ct);
        await db.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task UpdateAsync(UserSession session, CancellationToken ct = default)
    {
        db.UserSessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    public async IAsyncEnumerable<UserSession> ListByUserAsync(Guid userId, bool onlyActive = true, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = db.UserSessions.AsNoTracking().Where(s => s.UserId == userId);
        if (onlyActive)
            query = query.Where(s => !s.Revoked && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));

        await foreach (var s in query.OrderByDescending(s => s.CreatedAt).AsAsyncEnumerable().WithCancellation(ct))
            yield return s;
    }
}
