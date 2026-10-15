using FinPulse.Domain.Common;
using FinPulse.Domain.Enums;
using FinPulse.Domain.Events;
using FinPulse.Domain.Exceptions;

namespace FinPulse.Domain.Entities;

public class Wallet : Entity<Guid>, IAuditableEntity
{
    public Guid UserId { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public CurrencyCode Currency { get; private set; } = CurrencyCode.USD;
    public decimal Balance { get; private set; } = 0.00m;
    public WalletStatus Status { get; private set; } = WalletStatus.Active;
    public WalletTier Tier { get; private set; } = WalletTier.Standard;
    public string? FreezeReason { get; private set; }

    /// <summary>
    /// ConcurrencyToken ensures Optimistic Concurrency Control (OCC)
    /// preventing lost updates and race conditions during simultaneous debits/transfers.
    /// </summary>
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    // Navigation property
    public User? User { get; private set; }
    public List<Transaction> Transactions { get; private set; } = new();

    // Auditing
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private Wallet() { } // EF Core constructor

    public Wallet(Guid id, Guid userId, string accountNumber, CurrencyCode currency, WalletTier tier = WalletTier.Standard)
    {
        Id = id;
        UserId = userId;
        AccountNumber = accountNumber;
        Currency = currency;
        Tier = tier;
        Balance = 0.00m;
        Status = WalletStatus.Active;
        ConcurrencyToken = Guid.NewGuid();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public decimal GetDailyLimit() => Tier switch
    {
        WalletTier.Standard => 5_000.00m,
        WalletTier.Premium => 25_000.00m,
        WalletTier.VIP => 100_000.00m,
        _ => 5_000.00m
    };

    public void Deposit(decimal amount, string reference, string? note = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be strictly greater than zero.", nameof(amount));

        if (Status != WalletStatus.Active)
            throw new WalletFrozenException(AccountNumber);

        Balance += amount;
        RotateConcurrencyToken();

        AddDomainEvent(new FundsDepositedEvent(Id, amount, Currency, reference, DateTime.UtcNow));
    }

    public void Debit(decimal amount, string reference, string? note = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Debit amount must be strictly greater than zero.", nameof(amount));

        if (Status == WalletStatus.Frozen)
            throw new WalletFrozenException(AccountNumber);

        if (Status == WalletStatus.Closed)
            throw new InvalidOperationException($"Wallet '{AccountNumber}' is closed.");

        if (Balance < amount)
            throw new InsufficientFundsException(amount, Balance);

        var limit = GetDailyLimit();
        if (amount > limit)
            throw new DailyLimitExceededException(limit, amount);

        Balance -= amount;
        RotateConcurrencyToken();

        AddDomainEvent(new FundsWithdrawnEvent(Id, amount, Currency, reference, DateTime.UtcNow));
    }

    public void Credit(decimal amount, string reference, string? note = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Credit amount must be strictly greater than zero.", nameof(amount));

        if (Status != WalletStatus.Active)
            throw new WalletFrozenException(AccountNumber);

        Balance += amount;
        RotateConcurrencyToken();
    }

    public void Freeze(string reason)
    {
        Status = WalletStatus.Frozen;
        FreezeReason = reason;
        RotateConcurrencyToken();
        AddDomainEvent(new WalletFrozenEvent(Id, reason, DateTime.UtcNow));
    }

    public void Activate()
    {
        Status = WalletStatus.Active;
        FreezeReason = null;
        RotateConcurrencyToken();
    }

    public void ChangeTier(WalletTier newTier)
    {
        Tier = newTier;
        RotateConcurrencyToken();
    }

    public void RotateConcurrencyToken()
    {
        ConcurrencyToken = Guid.NewGuid();
        LastModifiedAtUtc = DateTime.UtcNow;
    }
}
