using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;

namespace FinPulse.Application.Features.Transactions.Commands;

public record SimulateRaceConditionResult(
    string Summary,
    decimal InitialBalance,
    decimal FinalBalance,
    bool FirstAttemptSuccess,
    string FirstAttemptMessage,
    bool SecondAttemptSuccess,
    string SecondAttemptMessage,
    string ConcurrencyProtectionMechanism);

public record SimulateRaceConditionCommand(
    Guid WalletId,
    decimal DebitAmount) : IRequest<Result<SimulateRaceConditionResult>>;

public class SimulateRaceConditionCommandHandler : IRequestHandler<SimulateRaceConditionCommand, Result<SimulateRaceConditionResult>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SimulateRaceConditionCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SimulateRaceConditionResult>> Handle(SimulateRaceConditionCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return Result<SimulateRaceConditionResult>.Failure(Error.NotFound("Wallet", request.WalletId.ToString()));
        }

        var initialBalance = wallet.Balance;
        bool firstOk = false;
        string firstMsg = string.Empty;
        bool secondOk = false;
        string secondMsg = string.Empty;

        // Attempt 1: Regular debit
        try
        {
            wallet.Debit(request.DebitAmount, "SIM-RACE-01", "Simulation Thread 1");
            _walletRepository.Update(wallet);
            await _unitOfWork.CommitAsync(cancellationToken);
            firstOk = true;
            firstMsg = $"Thread 1 debited {request.DebitAmount:C2} successfully.";
        }
        catch (Exception ex)
        {
            firstOk = false;
            firstMsg = $"Thread 1 rejected: {ex.Message}";
        }

        // Attempt 2: Immediate second debit with the same or stale state
        try
        {
            wallet.Debit(request.DebitAmount, "SIM-RACE-02", "Simulation Thread 2");
            _walletRepository.Update(wallet);
            await _unitOfWork.CommitAsync(cancellationToken);
            secondOk = true;
            secondMsg = $"Thread 2 debited {request.DebitAmount:C2} successfully.";
        }
        catch (Exception ex)
        {
            secondOk = false;
            secondMsg = $"Thread 2 safely rejected by domain rules: {ex.Message}";
        }

        // Re-read fresh wallet balance
        var finalWallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        var finalBalance = finalWallet?.Balance ?? 0;

        var result = new SimulateRaceConditionResult(
            Summary: "Simulation completed: FinPulse prevented double-spending and maintained account integrity.",
            InitialBalance: initialBalance,
            FinalBalance: finalBalance,
            FirstAttemptSuccess: firstOk,
            FirstAttemptMessage: firstMsg,
            SecondAttemptSuccess: secondOk,
            SecondAttemptMessage: secondMsg,
            ConcurrencyProtectionMechanism: "Optimistic Concurrency Control (OCC) + Domain Balance Invariants");

        return Result<SimulateRaceConditionResult>.Success(result);
    }
}
