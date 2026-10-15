namespace FinPulse.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class InsufficientFundsException : DomainException
{
    public decimal RequestedAmount { get; }
    public decimal AvailableBalance { get; }

    public InsufficientFundsException(decimal requested, decimal available) 
        : base($"Insufficient funds. Requested: {requested:C2}, Available: {available:C2}")
    {
        RequestedAmount = requested;
        AvailableBalance = available;
    }
}

public class WalletFrozenException : DomainException
{
    public WalletFrozenException(string accountNumber) 
        : base($"Wallet '{accountNumber}' is frozen and cannot process debit transactions.") { }
}

public class DailyLimitExceededException : DomainException
{
    public decimal Limit { get; }
    public decimal AttemptedAmount { get; }

    public DailyLimitExceededException(decimal limit, decimal attempted)
        : base($"Daily transaction limit of {limit:C2} exceeded by transaction of {attempted:C2}.")
    {
        Limit = limit;
        AttemptedAmount = attempted;
    }
}

public class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(string sourceCurrency, string targetCurrency)
        : base($"Currency mismatch between source ({sourceCurrency}) and target ({targetCurrency}). Cross-currency conversion is not enabled for this transaction.") { }
}

public class WalletNotFoundException : DomainException
{
    public WalletNotFoundException(string identifier)
        : base($"Wallet '{identifier}' was not found.") { }
}

public class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message = "The resource was modified by another transaction. Please retry.")
        : base(message) { }
}
