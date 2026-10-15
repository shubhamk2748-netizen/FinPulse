using Microsoft.EntityFrameworkCore;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FinPulse.Infrastructure.Persistence;

namespace FinPulse.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly FinPulseDbContext _context;

    public UnitOfWork(FinPulseDbContext context)
    {
        _context = context;
    }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

public class UserRepository : IUserRepository
{
    private readonly FinPulseDbContext _context;

    public UserRepository(FinPulseDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }
}

public class WalletRepository : IWalletRepository
{
    private readonly FinPulseDbContext _context;

    public WalletRepository(FinPulseDbContext context)
    {
        _context = context;
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Wallets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public Task<Wallet?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return _context.Wallets.FirstOrDefaultAsync(w => w.AccountNumber == accountNumber.ToUpperInvariant(), cancellationToken);
    }

    public Task<List<Wallet>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Wallets
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Wallet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.Wallets.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        await _context.Wallets.AddAsync(wallet, cancellationToken);
    }

    public void Update(Wallet wallet)
    {
        _context.Wallets.Update(wallet);
    }
}

public class TransactionRepository : ITransactionRepository
{
    private readonly FinPulseDbContext _context;

    public TransactionRepository(FinPulseDbContext context)
    {
        _context = context;
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Transactions
            .Include(t => t.SourceWallet)
            .Include(t => t.DestinationWallet)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<Transaction?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        return _context.Transactions
            .Include(t => t.SourceWallet)
            .Include(t => t.DestinationWallet)
            .FirstOrDefaultAsync(t => t.ReferenceNumber == reference, cancellationToken);
    }

    public Task<List<Transaction>> GetRecentByWalletIdAsync(Guid walletId, int count = 10, CancellationToken cancellationToken = default)
    {
        return _context.Transactions
            .Where(t => t.SourceWalletId == walletId || t.DestinationWalletId == walletId)
            .OrderByDescending(t => t.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<Transaction> Items, int TotalCount)> GetPagedAsync(
        Guid? walletId,
        TransactionType? type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Transactions.AsQueryable();

        if (walletId.HasValue)
        {
            query = query.Where(t => t.SourceWalletId == walletId.Value || t.DestinationWalletId == walletId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.TimestampUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.Transactions.AddAsync(transaction, cancellationToken);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly FinPulseDbContext _context;

    public AuditLogRepository(FinPulseDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }

    public Task<List<AuditLog>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        return _context.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly FinPulseDbContext _context;

    public IdempotencyRepository(FinPulseDbContext context)
    {
        _context = context;
    }

    public Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return _context.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.Key == key && i.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
    }

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }
}
