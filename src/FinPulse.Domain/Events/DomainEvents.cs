using FinPulse.Domain.Common;
using FinPulse.Domain.Enums;

namespace FinPulse.Domain.Events;

public record FundsDepositedEvent(
    Guid WalletId,
    decimal Amount,
    CurrencyCode Currency,
    string Reference,
    DateTime OccurredOnUtc) : IDomainEvent;

public record FundsTransferredEvent(
    Guid SourceWalletId,
    Guid DestinationWalletId,
    decimal Amount,
    CurrencyCode Currency,
    string Reference,
    DateTime OccurredOnUtc) : IDomainEvent;

public record FundsWithdrawnEvent(
    Guid WalletId,
    decimal Amount,
    CurrencyCode Currency,
    string Reference,
    DateTime OccurredOnUtc) : IDomainEvent;

public record WalletFrozenEvent(
    Guid WalletId,
    string Reason,
    DateTime OccurredOnUtc) : IDomainEvent;
