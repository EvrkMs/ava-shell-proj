using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Repositories;

public class TelegramRepository : ITelegramRepository
{
    private readonly AppDbContext _db;

    public TelegramRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsByTelegramIdAsync(long telegramId, CancellationToken ct = default) =>
        _db.TelegramEntities.AsNoTracking().AnyAsync(t => t.TelegramId == telegramId, ct);

    public Task<TelegramEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.TelegramEntities.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId, ct);

    public async Task AddAsync(TelegramEntity entity, CancellationToken ct = default)
    {
        _db.TelegramEntities.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(TelegramEntity entity, CancellationToken ct = default)
    {
        _db.TelegramEntities.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
