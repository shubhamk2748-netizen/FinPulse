using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FinPulse.Application.Common.Exceptions;
using FinPulse.Domain.Exceptions;

namespace FinPulse.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        _logger.LogError(exception, "[ERROR CAUGHT] Exception occurred on {Path}. TraceId: {TraceId}", httpContext.Request.Path, traceId);

        var (statusCode, title, detail, extensions) = exception switch
        {
            AppValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                "One or more validation errors occurred.",
                new Dictionary<string, object?> { ["errors"] = valEx.Errors }
            ),
            ConcurrencyConflictException concEx => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                concEx.Message,
                null
            ),
            InsufficientFundsException fundsEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Insufficient Funds",
                fundsEx.Message,
                new Dictionary<string, object?>
                {
                    ["requestedAmount"] = fundsEx.RequestedAmount,
                    ["availableBalance"] = fundsEx.AvailableBalance
                }
            ),
            WalletFrozenException frozenEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Wallet Frozen",
                frozenEx.Message,
                null
            ),
            DailyLimitExceededException limitEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Daily Limit Exceeded",
                limitEx.Message,
                new Dictionary<string, object?>
                {
                    ["limit"] = limitEx.Limit,
                    ["attemptedAmount"] = limitEx.AttemptedAmount
                }
            ),
            CurrencyMismatchException currEx => (
                StatusCodes.Status400BadRequest,
                "Currency Mismatch",
                currEx.Message,
                null
            ),
            DomainException domainEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Domain Rule Violation",
                domainEx.Message,
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected server error occurred. Please contact system support.",
                null
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        if (extensions != null)
        {
            foreach (var ext in extensions)
            {
                problemDetails.Extensions[ext.Key] = ext.Value;
            }
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
