using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Auth.Commands;
using FinPulse.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinPulse.Tests.Application;

public class AuthCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task RegisterUser_WhenValid_ShouldCreateUserAndReturnToken()
    {
        // Arrange
        var command = new RegisterUserCommand("newuser@finpulse.io", "New User", "Secret123!", "Customer");
        _userRepository.ExistsByEmailAsync("newuser@finpulse.io", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword("Secret123!").Returns("Hashed_Secret123!");
        _tokenGenerator.GenerateToken(Arg.Any<User>()).Returns("mock_jwt_token_abc");

        var handler = new RegisterUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator, _unitOfWork);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Email.Should().Be("newuser@finpulse.io");
        result.Value.FullName.Should().Be("New User");
        result.Value.Role.Should().Be("Customer");
        result.Value.Token.Should().Be("mock_jwt_token_abc");

        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => 
            u.Email == "newuser@finpulse.io" && 
            u.FullName == "New User" && 
            u.PasswordHash == "Hashed_Secret123!"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterUser_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var command = new RegisterUserCommand("existing@finpulse.io", "Existing User", "Secret123!");
        _userRepository.ExistsByEmailAsync("existing@finpulse.io", Arg.Any<CancellationToken>()).Returns(true);

        var handler = new RegisterUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator, _unitOfWork);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CONFLICT");
        result.Error.Message.Should().Contain("already exists");

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RegisterUserValidator_WithInvalidInputs_ShouldHaveValidationErrors()
    {
        // Arrange
        var validator = new RegisterUserCommandValidator();
        var emptyCommand = new RegisterUserCommand("", "", "");

        // Act
        var result = validator.Validate(emptyCommand);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task LoginUser_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "sarah@investor.com", "Sarah Jenkins", "hashed_pass", "Customer");
        _userRepository.GetByEmailAsync("sarah@investor.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("Sarah@12345", "hashed_pass").Returns(true);
        _tokenGenerator.GenerateToken(user).Returns("valid_sarah_jwt_token");

        var handler = new LoginUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator);
        var command = new LoginUserCommand("sarah@investor.com", "Sarah@12345");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("sarah@investor.com");
        result.Value.Token.Should().Be("valid_sarah_jwt_token");
    }

    [Fact]
    public async Task LoginUser_WithWrongPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "sarah@investor.com", "Sarah Jenkins", "hashed_pass", "Customer");
        _userRepository.GetByEmailAsync("sarah@investor.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("WrongPass", "hashed_pass").Returns(false);

        var handler = new LoginUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator);
        var command = new LoginUserCommand("sarah@investor.com", "WrongPass");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNAUTHORIZED");
        result.Error.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task LoginUser_WhenUserNotFound_ShouldReturnUnauthorized()
    {
        // Arrange
        _userRepository.GetByEmailAsync("nonexistent@finpulse.io", Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new LoginUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator);
        var command = new LoginUserCommand("nonexistent@finpulse.io", "Pass12345");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task LoginUser_WhenUserInactive_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "disabled@finpulse.io", "Disabled User", "hashed_pass", "Customer");
        user.Deactivate();

        _userRepository.GetByEmailAsync("disabled@finpulse.io", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("Pass12345", "hashed_pass").Returns(true);

        var handler = new LoginUserCommandHandler(_userRepository, _passwordHasher, _tokenGenerator);
        var command = new LoginUserCommand("disabled@finpulse.io", "Pass12345");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNAUTHORIZED");
        result.Error.Message.Should().Contain("inactive");
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ShouldReturnUserProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, "sarah@investor.com", "Sarah Jenkins", "hashed_pass", "Customer");

        _currentUserService.UserId.Returns(userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetCurrentUserQueryHandler(_currentUserService, _userRepository);
        var query = new GetCurrentUserQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Email.Should().Be("sarah@investor.com");
        result.Value.FullName.Should().Be("Sarah Jenkins");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUserService.UserId.Returns((Guid?)null);

        var handler = new GetCurrentUserQueryHandler(_currentUserService, _userRepository);
        var query = new GetCurrentUserQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNAUTHORIZED");
    }
}
