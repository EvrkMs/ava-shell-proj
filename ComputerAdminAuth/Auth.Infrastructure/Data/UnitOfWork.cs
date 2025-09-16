using Auth.Application.Interfaces;
using Auth.EntityFramework.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Auth.Infrastructure.Data;

// Auth.Infrastructure/Data/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfTransaction(transaction);
    }
}

// Обертка для EF транзакции
internal class EfTransaction : ITransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfTransaction(IDbContextTransaction transaction)
        => _transaction = transaction;

    public Task CommitAsync(CancellationToken ct = default)
        => _transaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default)
        => _transaction.RollbackAsync(ct);

    public void Dispose() => _transaction.Dispose();
}