using FluentValidation;
using MediatR;
using FinPulse.Application.Common;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Application.Features.Auth.DTOs;
using FinPulse.Domain.Entities;

namespace FinPulse.Application.Features.Auth.Commands;

public record RegisterUserCommand(
    string Email,
    string FullName,
    string Password,
    string? Role = "Customer") : IRequest<Result<AuthResponseDto>>;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MinimumLength(2).WithMessage("Full Name must have at least 2 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must have at least 6 characters.");
    }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponseDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(emailNormalized, cancellationToken))
        {
            return Result<AuthResponseDto>.Failure(Error.Conflict($"User with email '{emailNormalized}' already exists."));
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User(Guid.NewGuid(), emailNormalized, request.FullName, passwordHash, request.Role ?? "Customer");

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var token = _tokenGenerator.GenerateToken(user);
        var response = new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role, token);

        return Result<AuthResponseDto>.Success(response);
    }
}

public record LoginUserCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponseDto>>;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(emailNormalized, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<AuthResponseDto>.Failure(Error.Unauthorized("Invalid email or password credentials."));
        }

        if (!user.IsActive)
        {
            return Result<AuthResponseDto>.Failure(Error.Unauthorized("Account is inactive. Please contact support."));
        }

        var token = _tokenGenerator.GenerateToken(user);
        var response = new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role, token);

        return Result<AuthResponseDto>.Success(response);
    }
}

public record GetCurrentUserQuery : IRequest<Result<UserProfileDto>>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(ICurrentUserService currentUserService, IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Result<UserProfileDto>.Failure(Error.Unauthorized());
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user == null)
        {
            return Result<UserProfileDto>.Failure(Error.NotFound("User", _currentUserService.UserId.Value.ToString()));
        }

        var profile = new UserProfileDto(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAtUtc);
        return Result<UserProfileDto>.Success(profile);
    }
}
