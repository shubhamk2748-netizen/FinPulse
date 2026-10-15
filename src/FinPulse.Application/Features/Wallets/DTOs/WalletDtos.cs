using FinPulse.Domain.Enums;

namespace FinPulse.Application.Features.Wallets.DTOs;

public record WalletDto(
    Guid Id,
    Guid UserId,
    string AccountNumber,
    CurrencyCode Currency,
    string CurrencyName,
    decimal Balance,
    WalletStatus Status,
    string StatusName,
    WalletTier Tier,
    string TierName,
    decimal DailyLimit,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc);

public record WalletAnalyticsDto(
    decimal TotalBalanceUsdEquivalent,
    int TotalWallets,
    int ActiveWallets,
    int TotalTransactions,
    decimal TotalInflow,
    decimal TotalOutflow);
