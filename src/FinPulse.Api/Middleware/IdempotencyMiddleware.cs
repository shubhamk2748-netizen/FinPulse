using System.Security.Cryptography;
using System.Text;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Domain.Entities;

namespace FinPulse.Api.Middleware;

public class IdempotencyMiddleware
{
    private const string HeaderKey = "Idempotency-Key";
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyRepository repository, IUnitOfWork unitOfWork)
    {
        // Idempotency applies only to state-modifying HTTP methods (POST, PUT, PATCH, DELETE)
        if (!HttpMethods.IsPost(context.Request.Method) && 
            !HttpMethods.IsPut(context.Request.Method) && 
            !HttpMethods.IsPatch(context.Request.Method) &&
            !HttpMethods.IsDelete(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderKey, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = headerValue.ToString().Trim();

        // Compute hash of request body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var requestBody = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));

        // Check if idempotency key already exists
        var existingRecord = await repository.GetAsync(idempotencyKey, context.RequestAborted);
        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "IDEMPOTENCY_CONFLICT",
                    message = "Idempotency key was previously used with a different request payload."
                });
                return;
            }

            // Return cached response
            context.Response.Headers.Append("X-Idempotent-Replay", "true");
            context.Response.StatusCode = existingRecord.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existingRecord.ResponseBody);
            return;
        }

        // Buffer response to capture result
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            memoryStream.Position = 0;

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                var record = new IdempotencyRecord(
                    idempotencyKey,
                    requestHash,
                    context.Response.StatusCode,
                    responseBody,
                    TimeSpan.FromHours(24));

                await repository.AddAsync(record, context.RequestAborted);
                await unitOfWork.CommitAsync(context.RequestAborted);
            }

            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
