using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinPulse.Tests.Domain;

public class TransactionTests
{
    [Fact]
    public void CreateDeposit_ShouldSetCorrectProperties()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var amount = 1500.50m;
        var reference = "TXN-DEP-001";

        // Act
        var transaction = Transaction.CreateDeposit(walletId, amount, CurrencyCode.USD, reference, "Direct Deposit");

        // Assert
        transaction.DestinationWalletId.Should().Be(walletId);
        transaction.SourceWalletId.Should().BeNull();
        transaction.Amount.Should().Be(amount);
        transaction.Currency.Should().Be(CurrencyCode.USD);
        transaction.Type.Should().Be(TransactionType.Deposit);
        transaction.Status.Should().Be(TransactionStatus.Completed);
        transaction.ReferenceNumber.Should().Be(reference);
    }

    [Fact]
    public void CreateWithdrawal_ShouldSetCorrectProperties()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var amount = 250.00m;
        var reference = "TXN-WTH-001";

        // Act
        var transaction = Transaction.CreateWithdrawal(walletId, amount, CurrencyCode.EUR, reference, "ATM Withdrawal");

        // Assert
        transaction.SourceWalletId.Should().Be(walletId);
        transaction.DestinationWalletId.Should().BeNull();
        transaction.Amount.Should().Be(amount);
        transaction.Currency.Should().Be(CurrencyCode.EUR);
        transaction.Type.Should().Be(TransactionType.Withdrawal);
        transaction.Status.Should().Be(TransactionStatus.Completed);
    }

    [Fact]
    public void CreateTransfer_ShouldSetSourceAndDestination()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var amount = 99.99m;
        var reference = "TXN-XFER-001";

        // Act
        var transaction = Transaction.CreateTransfer(sourceId, destId, amount, CurrencyCode.GBP, reference, "P2P Payment");

        // Assert
        transaction.SourceWalletId.Should().Be(sourceId);
        transaction.DestinationWalletId.Should().Be(destId);
        transaction.Amount.Should().Be(amount);
        transaction.Currency.Should().Be(CurrencyCode.GBP);
        transaction.Type.Should().Be(TransactionType.Transfer);
        transaction.Status.Should().Be(TransactionStatus.Completed);
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatusAndFailureReason()
    {
        // Arrange
        var transaction = Transaction.CreateTransfer(Guid.NewGuid(), Guid.NewGuid(), 100m, CurrencyCode.USD, "TXN-001");

        // Act
        transaction.MarkAsFailed("Optimistic concurrency collision detected.");

        // Assert
        transaction.Status.Should().Be(TransactionStatus.Failed);
        transaction.FailureReason.Should().Be("Optimistic concurrency collision detected.");
    }
}
