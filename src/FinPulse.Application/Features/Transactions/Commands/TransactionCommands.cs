using FluentValidation;
using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Transactions.DTOs;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FinPulse.Domain.Exceptions;

namespace FinPulse.Application.Features.Transactions.Commands;

public record TransferFundsCommand(
    Guid SourceWalletId,
    string DestinationAccountNumber,
    decimal Amount,
    string? Description = null) : IRequest<Result<TransactionDto>>;

public class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        RuleFor(x => x.SourceWalletId)
            .NotEmpty().WithMessage("Source wallet ID is required.");

        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty().WithMessage("Destination account number is required.")
            .MinimumLength(5).WithMessage("Destination account number is invalid.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer amount must be strictly greater than 0.");
    }
}

public class TransferFundsCommandHandler : IRequestHandler<TransferFundsCommand, Result<TransactionDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public TransferFundsCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransactionDto>> Handle(TransferFundsCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch Source Wallet
        var sourceWallet = await _walletRepository.GetByIdAsync(request.SourceWalletId, cancellationToken);
        if (sourceWallet == null)
        {
            return Result<TransactionDto>.Failure(Error.NotFound("Source Wallet", request.SourceWalletId.ToString()));
        }

        // 2. Authorization check
        if (_currentUserService.UserId.HasValue && sourceWallet.UserId != _currentUserService.UserId && _currentUserService.Role != "Admin")
        {
            return Result<TransactionDto>.Failure(Error.Unauthorized("You do not have permission to transfer from this wallet."));
        }

        // 3. Fetch Destination Wallet
        var destinationWallet = await _walletRepository.GetByAccountNumberAsync(request.DestinationAccountNumber.Trim(), cancellationToken);
        if (destinationWallet == null)
        {
            return Result<TransactionDto>.Failure(Error.NotFound("Destination Account", request.DestinationAccountNumber));
        }

        // 4. Rule: Cannot transfer to the same wallet
        if (sourceWallet.Id == destinationWallet.Id)
        {
            return Result<TransactionDto>.Failure(Error.Validation("Source and destination wallets cannot be the same."));
        }

        // 5. Rule: Currencies must match (or conversion layer required)
        if (sourceWallet.Currency != destinationWallet.Currency)
        {
            return Result<TransactionDto>.Failure(Error.Validation(
                $"Currency mismatch. Source is '{sourceWallet.Currency}', destination is '{destinationWallet.Currency}'."));
        }

        var refNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";

        // 6. Execute Domain logic (encapsulates balance check, daily limit check, status check, OCC token rotation)
        try
        {
            sourceWallet.Debit(request.Amount, refNumber, request.Description);
            destinationWallet.Credit(request.Amount, refNumber, request.Description);
        }
        catch (DomainException dex)
        {
            return Result<TransactionDto>.Failure(Error.Validation(dex.Message));
        }

        // 7. Create immutable ledger record
        var transaction = Transaction.CreateTransfer(
            sourceWallet.Id,
            destinationWallet.Id,
            request.Amount,
            sourceWallet.Currency,
            refNumber,
            request.Description ?? "Peer-to-Peer Transfer");

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _walletRepository.Update(sourceWallet);
        _walletRepository.Update(destinationWallet);

        // 8. Commit with Optimistic Concurrency Control
        await _unitOfWork.CommitAsync(cancellationToken);

        var dto = new TransactionDto(
            transaction.Id,
            transaction.ReferenceNumber,
            sourceWallet.Id,
            sourceWallet.AccountNumber,
            destinationWallet.Id,
            destinationWallet.AccountNumber,
            transaction.Amount,
            transaction.Currency,
            transaction.Currency.ToString(),
            transaction.Type,
            transaction.Type.ToString(),
            transaction.Status,
            transaction.Status.ToString(),
            transaction.Description,
            transaction.FailureReason,
            transaction.TimestampUtc);

        return Result<TransactionDto>.Success(dto);
    }
}

public record DepositFundsCommand(
    Guid WalletId,
    decimal Amount,
    string? Description = null) : IRequest<Result<TransactionDto>>;

public class DepositFundsCommandValidator : AbstractValidator<DepositFundsCommand>
{
    public DepositFundsCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty().WithMessage("Wallet ID is required.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Deposit amount must be strictly greater than 0.");
    }
}

public class DepositFundsCommandHandler : IRequestHandler<DepositFundsCommand, Result<TransactionDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DepositFundsCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransactionDto>> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<TransactionDto>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        var refNumber = $"DEP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
        wallet.Deposit(request.Amount, refNumber, request.Description);

        var transaction = Transaction.CreateDeposit(
            wallet.Id,
            request.Amount,
            wallet.Currency,
            refNumber,
            request.Description ?? "Account Deposit");

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _walletRepository.Update(wallet);
        await _unitOfWork.CommitAsync(cancellationToken);

        var dto = new TransactionDto(
            transaction.Id,
            transaction.ReferenceNumber,
            null,
            null,
            wallet.Id,
            wallet.AccountNumber,
            transaction.Amount,
            transaction.Currency,
            transaction.Currency.ToString(),
            transaction.Type,
            transaction.Type.ToString(),
            transaction.Status,
            transaction.Status.ToString(),
            transaction.Description,
            transaction.FailureReason,
            transaction.TimestampUtc);

        return Result<TransactionDto>.Success(dto);
    }
}

public record WithdrawFundsCommand(
    Guid WalletId,
    decimal Amount,
    string? Description = null) : IRequest<Result<TransactionDto>>;

public class WithdrawFundsCommandValidator : AbstractValidator<WithdrawFundsCommand>
{
    public WithdrawFundsCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty().WithMessage("Wallet ID is required.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Withdrawal amount must be strictly greater than 0.");
    }
}

public class WithdrawFundsCommandHandler : IRequestHandler<WithdrawFundsCommand, Result<TransactionDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawFundsCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransactionDto>> Handle(WithdrawFundsCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<TransactionDto>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        var refNumber = $"WTH-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
        
        try
        {
            wallet.Debit(request.Amount, refNumber, request.Description);
        }
        catch (DomainException dex)
        {
            return Result<TransactionDto>.Failure(Error.Validation(dex.Message));
        }

        var transaction = Transaction.CreateWithdrawal(
            wallet.Id,
            request.Amount,
            wallet.Currency,
            refNumber,
            request.Description ?? "Account Withdrawal");

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _walletRepository.Update(wallet);
        await _unitOfWork.CommitAsync(cancellationToken);

        var dto = new TransactionDto(
            transaction.Id,
            transaction.ReferenceNumber,
            wallet.Id,
            wallet.AccountNumber,
            null,
            null,
            transaction.Amount,
            transaction.Currency,
            transaction.Currency.ToString(),
            transaction.Type,
            transaction.Type.ToString(),
            transaction.Status,
            transaction.Status.ToString(),
            transaction.Description,
            transaction.FailureReason,
            transaction.TimestampUtc);

        return Result<TransactionDto>.Success(dto);
    }
}
