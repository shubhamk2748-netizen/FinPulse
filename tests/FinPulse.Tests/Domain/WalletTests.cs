using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FinPulse.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FinPulse.Tests.Domain;

public class WalletTests
{
    [Fact]
    public void Constructor_ShouldInitializeWallet_WithCorrectDefaults()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountNumber = "WAL-USD-123456";

        // Act
        var wallet = new Wallet(walletId, userId, accountNumber, CurrencyCode.USD, WalletTier.Standard);

        // Assert
        wallet.Id.Should().Be(walletId);
        wallet.UserId.Should().Be(userId);
        wallet.AccountNumber.Should().Be(accountNumber);
        wallet.Currency.Should().Be(CurrencyCode.USD);
        wallet.Tier.Should().Be(WalletTier.Standard);
        wallet.Balance.Should().Be(0.00m);
        wallet.Status.Should().Be(WalletStatus.Active);
        wallet.ConcurrencyToken.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(WalletTier.Standard, 5_000.00)]
    [InlineData(WalletTier.Premium, 25_000.00)]
    [InlineData(WalletTier.VIP, 100_000.00)]
    public void GetDailyLimit_ShouldReturnTierLimit(WalletTier tier, decimal expectedLimit)
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD, tier);

        // Act & Assert
        wallet.GetDailyLimit().Should().Be(expectedLimit);
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalance_AndRotateConcurrencyToken()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);
        var initialToken = wallet.ConcurrencyToken;

        // Act
        wallet.Deposit(500.00m, "REF-001", "Test deposit");

        // Assert
        wallet.Balance.Should().Be(500.00m);
        wallet.ConcurrencyToken.Should().NotBe(initialToken);
    }

    [Fact]
    public void Deposit_ZeroOrNegative_ShouldThrowArgumentException()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);

        // Act & Assert
        var actZero = () => wallet.Deposit(0m, "REF-001");
        actZero.Should().Throw<ArgumentException>();

        var actNegative = () => wallet.Deposit(-50m, "REF-002");
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Debit_ValidAmount_ShouldDecreaseBalance_AndRotateToken()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);
        wallet.Deposit(1000.00m, "REF-001");
        var tokenBeforeDebit = wallet.ConcurrencyToken;

        // Act
        wallet.Debit(400.00m, "REF-002", "Grocery shopping");

        // Assert
        wallet.Balance.Should().Be(600.00m);
        wallet.ConcurrencyToken.Should().NotBe(tokenBeforeDebit);
    }

    [Fact]
    public void Debit_InsufficientBalance_ShouldThrowInsufficientFundsException()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);
        wallet.Deposit(100.00m, "REF-001");

        // Act & Assert
        var act = () => wallet.Debit(250.00m, "REF-002");
        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Debit_ExceedingDailyLimit_ShouldThrowDailyLimitExceededException()
    {
        // Arrange (Standard tier has $5,000 limit)
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD, WalletTier.Standard);
        wallet.Deposit(10_000.00m, "REF-001");

        // Act & Assert
        var act = () => wallet.Debit(6_000.00m, "REF-002");
        act.Should().Throw<DailyLimitExceededException>();
    }

    [Fact]
    public void Debit_FrozenWallet_ShouldThrowWalletFrozenException()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);
        wallet.Deposit(1000.00m, "REF-001");
        wallet.Freeze("Suspicious activity audit");

        // Act & Assert
        wallet.Status.Should().Be(WalletStatus.Frozen);
        var act = () => wallet.Debit(100.00m, "REF-002");
        act.Should().Throw<WalletFrozenException>();
    }

    [Fact]
    public void FreezeAndActivate_ShouldTransitionStatusProperly()
    {
        // Arrange
        var wallet = new Wallet(Guid.NewGuid(), Guid.NewGuid(), "WAL-USD-123456", CurrencyCode.USD);

        // Act Freeze
        wallet.Freeze("Risk review");
        wallet.Status.Should().Be(WalletStatus.Frozen);
        wallet.FreezeReason.Should().Be("Risk review");

        // Act Activate
        wallet.Activate();
        wallet.Status.Should().Be(WalletStatus.Active);
        wallet.FreezeReason.Should().BeNull();
    }
}
