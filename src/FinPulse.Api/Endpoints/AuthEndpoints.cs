using MediatR;
using FinPulse.Application.Features.Auth.Commands;

namespace FinPulse.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .WithName("RegisterUser")
        .WithSummary("Registers a new customer account.")
        .AllowAnonymous();

        group.MapPost("/login", async (LoginUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.Unauthorized();
        })
        .WithName("LoginUser")
        .WithSummary("Authenticates user credentials and returns a JWT token.")
        .AllowAnonymous();

        group.MapGet("/me", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCurrentUserQuery());
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.Unauthorized();
        })
        .WithName("GetCurrentUser")
        .WithSummary("Retrieves the currently authenticated user's profile.")
        .RequireAuthorization();

        return group;
    }
}
