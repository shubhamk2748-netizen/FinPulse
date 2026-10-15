using MediatR;
using FinPulse.Application.Features.Transactions.Commands;
using FinPulse.Application.Features.Transactions.Queries;
using FinPulse.Domain.Enums;

namespace FinPulse.Api.Endpoints;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/transfer", async (TransferFundsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.UnprocessableEntity(result.Error);
        })
        .WithName("TransferFunds")
        .WithSummary("Transfers funds between accounts with atomic balance updates and OCC verification.")
        .RequireAuthorization();

        group.MapPost("/deposit", async (DepositFundsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("DepositFunds")
        .WithSummary("Deposits funds into a wallet account.")
        .RequireAuthorization();

        group.MapPost("/withdraw", async (WithdrawFundsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.UnprocessableEntity(result.Error);
        })
        .WithName("WithdrawFunds")
        .WithSummary("Withdraws funds from a wallet account.")
        .RequireAuthorization();

        group.MapGet("/", async (
            Guid? walletId,
            TransactionType? type,
            int? page,
            int? pageSize,
            ISender sender) =>
        {
            var query = new GetTransactionsQuery(
                walletId,
                type,
                page ?? 1,
                pageSize ?? 10);

            var result = await sender.Send(query);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("GetTransactions")
        .WithSummary("Retrieves paginated ledger transactions with optional wallet and type filtering.")
        .RequireAuthorization();

        group.MapGet("/audit-logs", async (int? count, ISender sender) =>
        {
            var result = await sender.Send(new GetAuditLogsQuery(count ?? 20));
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("GetAuditLogs")
        .WithSummary("Retrieves immutable audit logs of state modifications.")
        .RequireAuthorization();

        // Flagship interview demonstration endpoint
        group.MapPost("/simulate-race", async (SimulateRaceConditionRequest request, ISender sender) =>
        {
            var result = await sender.Send(new SimulateRaceConditionCommand(request.WalletId, request.DebitAmount));
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("SimulateRaceCondition")
        .WithSummary("Live interview demo: triggers concurrent debits to demonstrate Optimistic Concurrency Control and double-spend prevention.")
        .RequireAuthorization();

        return group;
    }
}

public record SimulateRaceConditionRequest(Guid WalletId, decimal DebitAmount);
