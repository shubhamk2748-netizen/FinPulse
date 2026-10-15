namespace FinPulse.Domain.Enums;

public enum CurrencyCode
{
    USD = 1,
    EUR = 2,
    GBP = 3,
    JPY = 4,
    CAD = 5,
    INR = 6
}

public enum WalletStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum WalletTier
{
    Standard = 1, // Daily limit: $5,000
    Premium = 2,  // Daily limit: $25,000
    VIP = 3       // Daily limit: $100,000
}

public enum TransactionType
{
    Deposit = 1,
    Withdrawal = 2,
    Transfer = 3
}

public enum TransactionStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    FundsDeposited = 4,
    FundsTransferred = 5,
    FundsWithdrawn = 6,
    WalletFrozen = 7,
    WalletActivated = 8
}
