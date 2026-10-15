using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Transactions.Commands;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinPulse.Tests.Application;

public class TransferFundsCommandHandlerTests
{
    private readonly IWalletRepository _walletRepository = Substitute.For<IWalletRepository>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task TransferFunds_WhenValid_ShouldDebitSourceAndCreditDestination()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceWallet = new Wallet(Guid.NewGuid(), userId, "WAL-USD-100", CurrencyCode.USD);
        sourceWallet.Deposit(1000m, "INIT");

        var destWallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-200", CurrencyCode.USD);

        _currentUserService.UserId.Returns(userId);
        _currentUserService.Role.Returns("Customer");

        _walletRepository.GetByIdAsync(sourceWallet.Id, Arg.Any<CancellationToken>()).Returns(sourceWallet);
        _walletRepository.GetByAccountNumberAsync(destWallet.AccountNumber, Arg.Any<CancellationToken>()).Returns(destWallet);

        var handler = new TransferFundsCommandHandler(_walletRepository, _transactionRepository, _currentUserService, _unitOfWork);
        var command = new TransferFundsCommand(sourceWallet.Id, destWallet.AccountNumber, 300m, "Payment for services");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(300m);
        sourceWallet.Balance.Should().Be(700m);
        destWallet.Balance.Should().Be(300m);

        _walletRepository.Received(1).Update(sourceWallet);
        _walletRepository.Received(1).Update(destWallet);
        await _transactionRepository.Received(1).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransferFunds_WhenSourceAndDestinationSame_ShouldReturnValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = new Wallet(Guid.NewGuid(), userId, "WAL-USD-100", CurrencyCode.USD);
        wallet.Deposit(500m, "INIT");

        _currentUserService.UserId.Returns(userId);
        _walletRepository.GetByIdAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(wallet);
        _walletRepository.GetByAccountNumberAsync(wallet.AccountNumber, Arg.Any<CancellationToken>()).Returns(wallet);

        var handler = new TransferFundsCommandHandler(_walletRepository, _transactionRepository, _currentUserService, _unitOfWork);
        var command = new TransferFundsCommand(wallet.Id, wallet.AccountNumber, 50m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("Source and destination wallets cannot be the same");
    }

    [Fact]
    public async Task TransferFunds_WhenCurrencyMismatch_ShouldReturnValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceWallet = new Wallet(Guid.NewGuid(), userId, "WAL-USD-100", CurrencyCode.USD);
        sourceWallet.Deposit(500m, "INIT");

        var destWallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-EUR-200", CurrencyCode.EUR);

        _currentUserService.UserId.Returns(userId);
        _walletRepository.GetByIdAsync(sourceWallet.Id, Arg.Any<CancellationToken>()).Returns(sourceWallet);
        _walletRepository.GetByAccountNumberAsync(destWallet.AccountNumber, Arg.Any<CancellationToken>()).Returns(destWallet);

        var handler = new TransferFundsCommandHandler(_walletRepository, _transactionRepository, _currentUserService, _unitOfWork);
        var command = new TransferFundsCommand(sourceWallet.Id, destWallet.AccountNumber, 50m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("Currency mismatch");
    }
}
