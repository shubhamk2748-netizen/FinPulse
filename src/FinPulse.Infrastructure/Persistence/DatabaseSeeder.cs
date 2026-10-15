using Microsoft.EntityFrameworkCore;
using FinPulse.Application.Common.Interfaces;
using FinPulse.Domain.Entities;
using FinPulse.Domain.Enums;

namespace FinPulse.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(FinPulseDbContext context, IPasswordHasher passwordHasher)
    {
        // Automatically ensure SQLite DB schema is created
        await context.Database.EnsureCreatedAsync();

        // Read seed passwords from env vars (set in Railway/production)
        // Fallback values are ONLY for local development
        var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? "Admin@LocalDev_2026!";
        var sarahPassword = Environment.GetEnvironmentVariable("SEED_SARAH_PASSWORD") ?? "Sarah@LocalDev_2026!";
        var alexPassword  = Environment.GetEnvironmentVariable("SEED_ALEX_PASSWORD")  ?? "Alex@LocalDev_2026!";
        var rahulPassword = Environment.GetEnvironmentVariable("SEED_RAHUL_PASSWORD") ?? "Rahul@LocalDev_2026!";
        var priyaPassword = Environment.GetEnvironmentVariable("SEED_PRIYA_PASSWORD") ?? "Priya@LocalDev_2026!";

        // Ensure Indian Rupee (INR) users exist
        if (!await context.Users.AnyAsync(u => u.Email == "rahul@finpulse.in"))
        {
            var rahul = new User(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "rahul@finpulse.in",
                "Rahul Sharma (India)",
                passwordHasher.HashPassword(rahulPassword),
                "Customer");

            var rahulInrWallet = new Wallet(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                rahul.Id,
                "WAL-INR-881920",
                CurrencyCode.INR,
                WalletTier.VIP);
            rahulInrWallet.Deposit(75_000.00m, "INIT-INR-DEP", "UPI / IMPS Initial Deposit");

            var priya = new User(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "priya@finpulse.in",
                "Priya Patel (India)",
                passwordHasher.HashPassword(priyaPassword),
                "Customer");

            var priyaInrWallet = new Wallet(
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                priya.Id,
                "WAL-INR-334411",
                CurrencyCode.INR,
                WalletTier.Standard);
            priyaInrWallet.Deposit(25_000.00m, "INIT-PRIYA-INR", "UPI Initial Deposit");

            context.Users.AddRange(rahul, priya);
            context.Wallets.AddRange(rahulInrWallet, priyaInrWallet);

            var tInr = Transaction.CreateDeposit(
                rahulInrWallet.Id,
                75_000.00m,
                CurrencyCode.INR,
                "TXN-20261012-004004",
                "UPI Autopay Initial Deposit");

            context.Transactions.Add(tInr);
            await context.SaveChangesAsync();
        }

        if (await context.Users.AnyAsync(u => u.Email == "admin@finpulse.io"))
        {
            return; // Already seeded base users
        }

        // 1. Seed Users
        var admin = new User(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "admin@finpulse.io",
            "System Administrator",
            passwordHasher.HashPassword(adminPassword),
            "Admin");

        var sarah = new User(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "sarah@investor.com",
            "Sarah Jenkins (Investor)",
            passwordHasher.HashPassword(sarahPassword),
            "Customer");

        var alex = new User(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "alex@trader.com",
            "Alex Mercer (Trader)",
            passwordHasher.HashPassword(alexPassword),
            "Customer");

        context.Users.AddRange(admin, sarah, alex);
        await context.SaveChangesAsync();

        // 2. Seed Wallets
        var sarahUsdWallet = new Wallet(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            sarah.Id,
            "WAL-USD-108274",
            CurrencyCode.USD,
            WalletTier.VIP);
        sarahUsdWallet.Deposit(25_000.00m, "INIT-USD-DEP", "Initial high-yield savings deposit");

        var sarahEurWallet = new Wallet(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            sarah.Id,
            "WAL-EUR-554192",
            CurrencyCode.EUR,
            WalletTier.Premium);
        sarahEurWallet.Deposit(12_500.00m, "INIT-EUR-DEP", "European portfolio allocation");

        var alexUsdWallet = new Wallet(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            alex.Id,
            "WAL-USD-992140",
            CurrencyCode.USD,
            WalletTier.Standard);
        alexUsdWallet.Deposit(10_000.00m, "INIT-ALX-USD", "Trading balance top-up");

        var alexGbpWallet = new Wallet(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            alex.Id,
            "WAL-GBP-448201",
            CurrencyCode.GBP,
            WalletTier.Standard);
        alexGbpWallet.Deposit(8_000.00m, "INIT-ALX-GBP", "London FX operating balance");

        context.Wallets.AddRange(sarahUsdWallet, sarahEurWallet, alexUsdWallet, alexGbpWallet);
        await context.SaveChangesAsync();

        // 3. Seed Realistic Transactions
        var t1 = Transaction.CreateDeposit(
            sarahUsdWallet.Id,
            25_000.00m,
            CurrencyCode.USD,
            "TXN-20261001-001001",
            "Initial capital deployment");

        var t2 = Transaction.CreateDeposit(
            alexUsdWallet.Id,
            10_000.00m,
            CurrencyCode.USD,
            "TXN-20261003-002002",
            "Direct bank wire deposit");

        // Transfer from Sarah to Alex ($1,500.00)
        sarahUsdWallet.Debit(1_500.00m, "TXN-20261005-003003", "Equity syndicate syndicate fee");
        alexUsdWallet.Credit(1_500.00m, "TXN-20261005-003003", "Equity syndicate syndicate fee");
        
        var t3 = Transaction.CreateTransfer(
            sarahUsdWallet.Id,
            alexUsdWallet.Id,
            1_500.00m,
            CurrencyCode.USD,
            "TXN-20261005-003003",
            "Equity syndicate investment contribution");

        context.Transactions.AddRange(t1, t2, t3);
        await context.SaveChangesAsync();

        // 4. Ensure Indian Rupee (INR) users exist
        if (!await context.Users.AnyAsync(u => u.Email == "rahul@finpulse.in"))
        {
            var rahul = new User(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "rahul@finpulse.in",
                "Rahul Sharma (India)",
                passwordHasher.HashPassword("Rahul@12345"),
                "Customer");

            var rahulInrWallet = new Wallet(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                rahul.Id,
                "WAL-INR-881920",
                CurrencyCode.INR,
                WalletTier.VIP);
            rahulInrWallet.Deposit(75_000.00m, "INIT-INR-DEP", "UPI / IMPS Initial Deposit");

            var priya = new User(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "priya@finpulse.in",
                "Priya Patel (India)",
                passwordHasher.HashPassword("Priya@12345"),
                "Customer");

            var priyaInrWallet = new Wallet(
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                priya.Id,
                "WAL-INR-334411",
                CurrencyCode.INR,
                WalletTier.Standard);
            priyaInrWallet.Deposit(25_000.00m, "INIT-PRIYA-INR", "UPI Initial Deposit");

            context.Users.AddRange(rahul, priya);
            context.Wallets.AddRange(rahulInrWallet, priyaInrWallet);

            var tInr = Transaction.CreateDeposit(
                rahulInrWallet.Id,
                75_000.00m,
                CurrencyCode.INR,
                "TXN-20261012-004004",
                "UPI Autopay Initial Deposit");

            context.Transactions.Add(tInr);
            await context.SaveChangesAsync();
        }
    }
}
