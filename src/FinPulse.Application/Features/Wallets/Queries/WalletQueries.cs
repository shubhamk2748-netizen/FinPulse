using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Wallets.Commands;
using FinPulse.Application.Features.Wallets.DTOs;
using FinPulse.Domain.Enums;

namespace FinPulse.Application.Features.Wallets.Queries;

public record GetMyWalletsQuery : IRequest<Result<List<WalletDto>>>;

public class GetMyWalletsQueryHandler : IRequestHandler<GetMyWalletsQuery, Result<List<WalletDto>>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyWalletsQueryHandler(IWalletRepository walletRepository, ICurrentUserService currentUserService)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<WalletDto>>> Handle(GetMyWalletsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Result<List<WalletDto>>.Failure(Error.Unauthorized());
        }

        var wallets = await _walletRepository.GetByUserIdAsync(_currentUserService.UserId.Value, cancellationToken);
        var dtos = wallets.Select(CreateWalletCommandHandler.MapToDto).ToList();

        return Result<List<WalletDto>>.Success(dtos);
    }
}

public record GetWalletByIdQuery(Guid WalletId) : IRequest<Result<WalletDto>>;

public class GetWalletByIdQueryHandler : IRequestHandler<GetWalletByIdQuery, Result<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetWalletByIdQueryHandler(IWalletRepository walletRepository, ICurrentUserService currentUserService)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WalletDto>> Handle(GetWalletByIdQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<WalletDto>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        if (wallet.UserId != _currentUserService.UserId && _currentUserService.Role != "Admin")
        {
            return Result<WalletDto>.Failure(Error.Unauthorized("Access to this wallet is restricted."));
        }

        return Result<WalletDto>.Success(CreateWalletCommandHandler.MapToDto(wallet));
    }
}

public record GetWalletAnalyticsQuery : IRequest<Result<WalletAnalyticsDto>>;

public class GetWalletAnalyticsQueryHandler : IRequestHandler<GetWalletAnalyticsQuery, Result<WalletAnalyticsDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetWalletAnalyticsQueryHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ICurrentUserService currentUserService)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WalletAnalyticsDto>> Handle(GetWalletAnalyticsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Result<WalletAnalyticsDto>.Failure(Error.Unauthorized());
        }

        var wallets = await _walletRepository.GetByUserIdAsync(_currentUserService.UserId.Value, cancellationToken);
        var totalBalance = wallets.Sum(w => w.Balance);
        var activeWallets = wallets.Count(w => w.Status == WalletStatus.Active);

        // Fetch recent transactions to calculate metrics
        var (txns, totalCount) = await _transactionRepository.GetPagedAsync(null, null, 1, 100, cancellationToken);
        var walletIds = wallets.Select(w => w.Id).ToHashSet();
        
        var userTxns = txns.Where(t => 
            (t.SourceWalletId.HasValue && walletIds.Contains(t.SourceWalletId.Value)) ||
            (t.DestinationWalletId.HasValue && walletIds.Contains(t.DestinationWalletId.Value))).ToList();

        var totalInflow = userTxns
            .Where(t => t.DestinationWalletId.HasValue && walletIds.Contains(t.DestinationWalletId.Value))
            .Sum(t => t.Amount);

        var totalOutflow = userTxns
            .Where(t => t.SourceWalletId.HasValue && walletIds.Contains(t.SourceWalletId.Value))
            .Sum(t => t.Amount);

        var analytics = new WalletAnalyticsDto(
            TotalBalanceUsdEquivalent: totalBalance,
            TotalWallets: wallets.Count,
            ActiveWallets: activeWallets,
            TotalTransactions: userTxns.Count,
            TotalInflow: totalInflow,
            TotalOutflow: totalOutflow);

        return Result<WalletAnalyticsDto>.Success(analytics);
    }
}
