using FluentValidation;
using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Wallets.DTOs;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;

namespace FinPulse.Application.Features.Wallets.Commands;

public record CreateWalletCommand(
    CurrencyCode Currency,
    WalletTier Tier = WalletTier.Standard) : IRequest<Result<WalletDto>>;

public class CreateWalletCommandValidator : AbstractValidator<CreateWalletCommand>
{
    public CreateWalletCommandValidator()
    {
        RuleFor(x => x.Currency)
            .IsInEnum().WithMessage("A valid currency code must be provided.");

        RuleFor(x => x.Tier)
            .IsInEnum().WithMessage("A valid wallet tier must be provided.");
    }
}

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, Result<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWalletCommandHandler(
        IWalletRepository walletRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WalletDto>> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Result<WalletDto>.Failure(Error.Unauthorized());
        }

        var randomCode = Random.Shared.Next(100000, 999999);
        var accountNumber = $"WAL-{request.Currency}-{randomCode}";

        var wallet = new Wallet(
            Guid.NewGuid(),
            _currentUserService.UserId.Value,
            accountNumber,
            request.Currency,
            request.Tier);

        await _walletRepository.AddAsync(wallet, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<WalletDto>.Success(MapToDto(wallet));
    }

    internal static WalletDto MapToDto(Wallet w) => new(
        w.Id,
        w.UserId,
        w.AccountNumber,
        w.Currency,
        w.Currency.ToString(),
        w.Balance,
        w.Status,
        w.Status.ToString(),
        w.Tier,
        w.Tier.ToString(),
        w.GetDailyLimit(),
        w.ConcurrencyToken,
        w.CreatedAtUtc);
}

public record FreezeWalletCommand(Guid WalletId, string Reason) : IRequest<Result<WalletDto>>;

public class FreezeWalletCommandHandler : IRequestHandler<FreezeWalletCommand, Result<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public FreezeWalletCommandHandler(
        IWalletRepository walletRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WalletDto>> Handle(FreezeWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<WalletDto>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        if (wallet.UserId != _currentUserService.UserId && _currentUserService.Role != "Admin")
        {
            return Result<WalletDto>.Failure(Error.Unauthorized("You do not have permission to freeze this wallet."));
        }

        wallet.Freeze(string.IsNullOrWhiteSpace(request.Reason) ? "Administrative freeze" : request.Reason);
        _walletRepository.Update(wallet);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<WalletDto>.Success(CreateWalletCommandHandler.MapToDto(wallet));
    }
}

public record ActivateWalletCommand(Guid WalletId) : IRequest<Result<WalletDto>>;

public class ActivateWalletCommandHandler : IRequestHandler<ActivateWalletCommand, Result<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateWalletCommandHandler(
        IWalletRepository walletRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WalletDto>> Handle(ActivateWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<WalletDto>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        if (wallet.UserId != _currentUserService.UserId && _currentUserService.Role != "Admin")
        {
            return Result<WalletDto>.Failure(Error.Unauthorized("You do not have permission to activate this wallet."));
        }

        wallet.Activate();
        _walletRepository.Update(wallet);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<WalletDto>.Success(CreateWalletCommandHandler.MapToDto(wallet));
    }
}
