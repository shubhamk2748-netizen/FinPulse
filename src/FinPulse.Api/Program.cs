using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using FinPulse.Application;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Infrastructure;
using FinPulse.Infrastructure.Persistence;
using FinPulse.Api.Endpoints;
using FinPulse.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Application & Infrastructure services (Clean Architecture)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Exception Handling (RFC 7807 ProblemDetails via IExceptionHandler)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Authentication & Authorization (JWT Bearer)
var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "FinPulseEnterpriseSecretKey_2026_HighSecuritySignature_!#992";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FinPulse.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FinPulse.App";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. Swagger / OpenAPI Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FinPulse API - High-Throughput Digital Wallet Engine",
        Version = "v1",
        Description = "Enterprise Digital Wallet & Financial Transaction Processing API with CQRS, Optimistic Concurrency Control, and Idempotency Guarantees."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
});

// 5. CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 6. Automatic Database Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FinPulseDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DatabaseSeeder.SeedAsync(dbContext, passwordHasher);
}

// 7. HTTP Pipeline Middleware
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.UseCors();

// Host the built-in UI Dashboard from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Swagger API Documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinPulse API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// 8. Register Minimal API Endpoints
app.MapGroup("/api/auth").MapAuthEndpoints();
app.MapGroup("/api/wallets").MapWalletEndpoints();
app.MapGroup("/api/transactions").MapTransactionEndpoints();
app.MapGroup("/api/system").MapSystemEndpoints();

app.Run();
