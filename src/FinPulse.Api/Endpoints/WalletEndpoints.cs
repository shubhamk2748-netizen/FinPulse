using MediatR;
using FinPulse.Application.Features.Wallets.Commands;
using FinPulse.Application.Features.Wallets.Queries;

namespace FinPulse.Api.Endpoints;

public static class WalletEndpoints
{
    public static RouteGroupBuilder MapWalletEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateWalletCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Created($"/api/wallets/{result.Value.Id}", result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateWallet")
        .WithSummary("Creates a new multi-currency wallet account.")
        .RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyWalletsQuery());
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("GetMyWallets")
        .WithSummary("Retrieves all wallets owned by the authenticated user.")
        .RequireAuthorization();

        group.MapGet("/analytics", async (ISender sender) =>
        {
            var result = await sender.Send(new GetWalletAnalyticsQuery());
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("GetWalletAnalytics")
        .WithSummary("Retrieves aggregate balance, transaction counts, and inflow/outflow metrics.")
        .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWalletByIdQuery(id));
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.NotFound(result.Error);
        })
        .WithName("GetWalletById")
        .WithSummary("Retrieves details and live balance of a specific wallet.")
        .RequireAuthorization();

        group.MapPost("/{id:guid}/freeze", async (Guid id, FreezeWalletRequest request, ISender sender) =>
        {
            var result = await sender.Send(new FreezeWalletCommand(id, request.Reason));
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("FreezeWallet")
        .WithSummary("Freezes a wallet to prevent debit transactions.")
        .RequireAuthorization();

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new ActivateWalletCommand(id));
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("ActivateWallet")
        .WithSummary("Activates a frozen wallet.")
        .RequireAuthorization();

        return group;
    }
}

public record FreezeWalletRequest(string Reason);
