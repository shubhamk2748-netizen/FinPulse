using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Transactions.Commands;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinPulse.Tests.Application;

public class SimulateRaceConditionTests
{
    private readonly IWalletRepository _walletRepository = Substitute.For<IWalletRepository>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task SimulateRaceCondition_WhenBalanceSufficientForOnlyOne_SecondShouldFailSafely()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var wallet = new Wallet(walletId, Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);
        wallet.Deposit(100.00m, "INIT"); // Initial balance $100

        _walletRepository.GetByIdAsync(walletId, Arg.Any<CancellationToken>()).Returns(wallet);

        var handler = new SimulateRaceConditionCommandHandler(_walletRepository, _transactionRepository, _unitOfWork);
        var command = new SimulateRaceConditionCommand(walletId, 80.00m); // Try debiting $80 twice

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InitialBalance.Should().Be(100.00m);
        result.Value.FinalBalance.Should().Be(20.00m); // 100 - 80 = 20
        result.Value.FirstAttemptSuccess.Should().BeTrue();
        result.Value.SecondAttemptSuccess.Should().BeFalse();
        result.Value.SecondAttemptMessage.Should().Contain("safely rejected by domain rules");
    }
}
