namespace FinPulse.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token);

public record UserProfileDto(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc);
