# ── Stage 1: Build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and restore
COPY FinPulse.slnx ./
COPY src/FinPulse.Domain/FinPulse.Domain.csproj             src/FinPulse.Domain/
COPY src/FinPulse.Application/FinPulse.Application.csproj   src/FinPulse.Application/
COPY src/FinPulse.Infrastructure/FinPulse.Infrastructure.csproj src/FinPulse.Infrastructure/
COPY src/FinPulse.Api/FinPulse.Api.csproj                   src/FinPulse.Api/
COPY tests/FinPulse.Tests/FinPulse.Tests.csproj             tests/FinPulse.Tests/

RUN dotnet restore src/FinPulse.Api/FinPulse.Api.csproj

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/FinPulse.Api/FinPulse.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Railway injects PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "FinPulse.Api.dll"]
