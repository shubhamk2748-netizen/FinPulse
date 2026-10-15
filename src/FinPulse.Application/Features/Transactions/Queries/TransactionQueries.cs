using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Transactions.DTOs;
using FinPulse.Domain.Enums;

namespace FinPulse.Application.Features.Transactions.Queries;

public record GetTransactionsQuery(
    Guid? WalletId = null,
    TransactionType? Type = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedResult<TransactionDto>>>;

public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, Result<PagedResult<TransactionDto>>>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;

    public GetTransactionsQueryHandler(
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository)
    {
        _transactionRepository = transactionRepository;
        _walletRepository = walletRepository;
    }

    public async Task<Result<PagedResult<TransactionDto>>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (txns, totalCount) = await _transactionRepository.GetPagedAsync(
            request.WalletId,
            request.Type,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        // Pre-fetch wallet account numbers for source and destination
        var allWallets = await _walletRepository.GetAllAsync(cancellationToken);
        var walletMap = allWallets.ToDictionary(w => w.Id, w => w.AccountNumber);

        var dtos = txns.Select(t => new TransactionDto(
            t.Id,
            t.ReferenceNumber,
            t.SourceWalletId,
            t.SourceWalletId.HasValue && walletMap.TryGetValue(t.SourceWalletId.Value, out var src) ? src : null,
            t.DestinationWalletId,
            t.DestinationWalletId.HasValue && walletMap.TryGetValue(t.DestinationWalletId.Value, out var dst) ? dst : null,
            t.Amount,
            t.Currency,
            t.Currency.ToString(),
            t.Type,
            t.Type.ToString(),
            t.Status,
            t.Status.ToString(),
            t.Description,
            t.FailureReason,
            t.TimestampUtc)).ToList();

        var pagedResult = new PagedResult<TransactionDto>(dtos, totalCount, request.PageNumber, request.PageSize);
        return Result<PagedResult<TransactionDto>>.Success(pagedResult);
    }
}

public record GetAuditLogsQuery(int Count = 20) : IRequest<Result<List<AuditLogDto>>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<List<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<List<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _auditLogRepository.GetRecentAsync(request.Count, cancellationToken);
        var dtos = logs.Select(l => new AuditLogDto(
            l.Id,
            l.EntityName,
            l.EntityId,
            l.Action,
            l.Action.ToString(),
            l.PerformedBy ?? "System",
            l.ChangesJson,
            l.TimestampUtc)).ToList();

        return Result<List<AuditLogDto>>.Success(dtos);
    }
}
