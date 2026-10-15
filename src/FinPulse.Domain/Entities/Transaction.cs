using FinPulse.Domain.Common;
using FinPulse.Domain.Enums;

namespace FinPulse.Domain.Entities;

public class Transaction : Entity<Guid>
{
    public string ReferenceNumber { get; private set; } = string.Empty;
    public Guid? SourceWalletId { get; private set; }
    public Guid? DestinationWalletId { get; private set; }
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    // Navigation properties
    public Wallet? SourceWallet { get; private set; }
    public Wallet? DestinationWallet { get; private set; }

    private Transaction() { } // EF Core constructor

    public static Transaction CreateDeposit(
        Guid destinationWalletId,
        decimal amount,
        CurrencyCode currency,
        string referenceNumber,
        string description = "Funds Deposit")
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = referenceNumber,
            DestinationWalletId = destinationWalletId,
            Amount = amount,
            Currency = currency,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            Description = description,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public static Transaction CreateWithdrawal(
        Guid sourceWalletId,
        decimal amount,
        CurrencyCode currency,
        string referenceNumber,
        string description = "Funds Withdrawal")
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = referenceNumber,
            SourceWalletId = sourceWalletId,
            Amount = amount,
            Currency = currency,
            Type = TransactionType.Withdrawal,
            Status = TransactionStatus.Completed,
            Description = description,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public static Transaction CreateTransfer(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        CurrencyCode currency,
        string referenceNumber,
        string description = "Peer-to-Peer Transfer")
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = referenceNumber,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = amount,
            Currency = currency,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Description = description,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public void MarkAsFailed(string reason)
    {
        Status = TransactionStatus.Failed;
        FailureReason = reason;
    }
}
