namespace FinPulse.Domain.Entities;

public class IdempotencyRecord
{
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; private set; }

    private IdempotencyRecord() { } // EF Core constructor

    public IdempotencyRecord(string key, string requestHash, int statusCode, string responseBody, TimeSpan timeToLive)
    {
        Key = key;
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = DateTime.UtcNow.Add(timeToLive);
    }
}
