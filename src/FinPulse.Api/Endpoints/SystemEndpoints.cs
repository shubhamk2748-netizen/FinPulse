using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Infrastructure.Persistence;

namespace FinPulse.Api.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;

    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", async (FinPulseDbContext dbContext) =>
        {
            var dbHealthy = false;
            string? dbError = null;

            try
            {
                dbHealthy = await dbContext.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                dbError = ex.Message;
            }

            var memoryUsedMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            var uptime = DateTime.UtcNow - StartTimeUtc;

            return Results.Ok(new
            {
                status = dbHealthy ? "Healthy" : "Degraded",
                timestampUtc = DateTime.UtcNow,
                uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                database = new
                {
                    provider = "SQLite / EF Core",
                    connected = dbHealthy,
                    error = dbError
                },
                process = new
                {
                    memoryUsageMb = memoryUsedMb,
                    dotnetVersion = Environment.Version.ToString(),
                    osArchitecture = Environment.OSVersion.VersionString
                }
            });
        })
        .WithName("SystemHealth")
        .WithSummary("System health check assessing database connectivity and runtime metrics.")
        .AllowAnonymous();

        group.MapGet("/info", () =>
        {
            return Results.Ok(new
            {
                application = "FinPulse Enterprise Wallet & Transaction Engine",
                version = "1.0.0-LTS",
                architecture = "Clean Architecture / CQRS / MediatR / FluentValidation",
                concurrencyStrategy = "Optimistic Concurrency Control (OCC) with RowVersion Concurrency Token",
                idempotencyStrategy = "Header-based RFC Idempotency Key Caching with SHA-256 Payload Hash",
                errorFormat = "RFC 7807 ProblemDetails via IExceptionHandler",
                supportedCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "CAD", "INR" },
                walletTiers = new[]
                {
                    new { tier = "Standard", dailyLimit = 5000.00 },
                    new { tier = "Premium", dailyLimit = 25000.00 },
                    new { tier = "VIP", dailyLimit = 100000.00 }
                }
            });
        })
        .WithName("SystemInfo")
        .WithSummary("Architectural and metadata summary of the FinPulse engine.")
        .AllowAnonymous();

        return group;
    }
}
