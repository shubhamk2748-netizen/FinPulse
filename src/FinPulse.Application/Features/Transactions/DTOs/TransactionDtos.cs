using FinPulse.Domain.Enums;

namespace FinPulse.Application.Features.Transactions.DTOs;

public record TransactionDto(
    Guid Id,
    string ReferenceNumber,
    Guid? SourceWalletId,
    string? SourceAccountNumber,
    Guid? DestinationWalletId,
    string? DestinationAccountNumber,
    decimal Amount,
    CurrencyCode Currency,
    string CurrencyName,
    TransactionType Type,
    string TypeName,
    TransactionStatus Status,
    string StatusName,
    string Description,
    string? FailureReason,
    DateTime TimestampUtc);

public record AuditLogDto(
    Guid Id,
    string EntityName,
    string EntityId,
    AuditAction Action,
    string ActionName,
    string PerformedBy,
    string ChangesJson,
    DateTime TimestampUtc);
