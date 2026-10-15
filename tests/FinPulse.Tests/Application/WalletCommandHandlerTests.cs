using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Wallets.Commands;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinPulse.Tests.Application;

public class WalletCommandHandlerTests
{
    private readonly IWalletRepository _walletRepository = Substitute.For<IWalletRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task CreateWallet_WithAuthenticatedUser_ShouldCreateWallet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId);

        var handler = new CreateWalletCommandHandler(_walletRepository, _currentUserService, _unitOfWork);
        var command = new CreateWalletCommand(CurrencyCode.USD, WalletTier.Premium);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.Currency.Should().Be(CurrencyCode.USD);
        result.Value.Tier.Should().Be(WalletTier.Premium);
        result.Value.Balance.Should().Be(0m);

        await _walletRepository.Received(1).AddAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWallet_WhenUnauthorized_ShouldReturnFailure()
    {
        // Arrange
        _currentUserService.UserId.Returns((Guid?)null);

        var handler = new CreateWalletCommandHandler(_walletRepository, _currentUserService, _unitOfWork);
        var command = new CreateWalletCommand(CurrencyCode.EUR);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task FreezeWallet_ExistingWallet_ShouldFreezeSuccessfully()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var wallet = new Wallet(walletId, userId, "WAL-USD-111", CurrencyCode.USD);
        _currentUserService.UserId.Returns(userId);
        _walletRepository.GetByIdAsync(walletId, Arg.Any<CancellationToken>()).Returns(wallet);

        var handler = new FreezeWalletCommandHandler(_walletRepository, _currentUserService, _unitOfWork);
        var command = new FreezeWalletCommand(walletId, "Compliance check");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(WalletStatus.Frozen);
        wallet.Status.Should().Be(WalletStatus.Frozen);

        _walletRepository.Received(1).Update(wallet);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
