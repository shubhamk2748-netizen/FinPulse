# FinPulse 💳
### High-Throughput Digital Wallet & Financial Ledger Engine

A production-ready **Fintech REST API** built with **.NET 10 / ASP.NET Core**, implementing enterprise-grade patterns for safe, concurrent financial transactions.

---

## ✨ Features

| Feature | Details |
|---|---|
| 🔐 JWT Authentication | Role-based access (Admin / User) |
| 👛 Wallet Management | Create, top-up, deactivate wallets |
| 💸 P2P Fund Transfers | Instant transfers with balance validation |
| 🔁 Idempotency | SHA-256 keyed middleware — no duplicate charges |
| ⚡ Race Condition Protection | Optimistic Concurrency Control (EF Core RowVersion) |
| 📋 Audit Logging | Automatic JSON change-tracking on every entity |
| 🧪 31+ Tests | Unit & concurrency integration tests (xUnit + NSubstitute) |

---

## 🏗️ Architecture

```
Clean Architecture  +  CQRS (MediatR)
┌──────────────────────────────────────────┐
│  FinPulse.Api          (Endpoints, Middleware)        │
│  FinPulse.Application  (Commands, Queries, Behaviors) │
│  FinPulse.Domain       (Entities, DDD, Events)        │
│  FinPulse.Infrastructure (EF Core, JWT, Repos)        │
└──────────────────────────────────────────┘
```

## 🛠️ Tech Stack

- **Runtime:** .NET 10 / ASP.NET Core
- **ORM:** Entity Framework Core (SQLite for dev / SQL Server ready)
- **Mediator:** MediatR + Pipeline Behaviors
- **Auth:** JWT Bearer Tokens
- **Testing:** xUnit, NSubstitute, FluentAssertions
- **API Docs:** Scalar (OpenAPI)

---

## 🚀 Getting Started

### 1. Clone & configure

```bash
git clone https://github.com/YOUR_USERNAME/FinPulse.git
cd FinPulse
cp src/FinPulse.Api/appsettings.example.json src/FinPulse.Api/appsettings.json
# Edit appsettings.json — add your JWT SecretKey (min 32 chars)
```

### 2. Run

```bash
dotnet run --project src/FinPulse.Api
# API + UI → http://localhost:5000
```

### 3. Test

```bash
dotnet test
```

---

## 📡 API Endpoints

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login → JWT token |
| GET | `/api/wallets` | List user wallets |
| POST | `/api/wallets` | Create wallet |
| POST | `/api/wallets/{id}/topup` | Add funds |
| POST | `/api/transactions/transfer` | P2P transfer |
| GET | `/api/transactions` | Transaction history |

---

## 🔒 Security Notes

- Never commit `appsettings.json` — use `appsettings.example.json` as template
- JWT SecretKey must be at least 32 characters
- SQLite DB file is git-ignored

---

## 📄 License

MIT
