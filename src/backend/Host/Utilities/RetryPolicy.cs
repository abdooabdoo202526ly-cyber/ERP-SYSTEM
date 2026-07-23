using Microsoft.Extensions.Logging;
using Npgsql;

namespace ERPSystem.Host.Utilities;

/// <summary>
/// Retry policy مخصص لـ Postgres + Dapper operations (بدون Polly لتفادي dependency).
/// Exponential backoff: 2^attempt seconds (1s, 2s, 4s).
///
/// Sprint-4 Day 2 (DEC-042).
/// </summary>
public static class RetryPolicy
{
    public const int DefaultMaxRetries = 3;

    /// <summary>
    /// ينفّذ operation مع retry تلقائي على transient DB errors.
    /// يُعيد المحاولة عند:
    ///   - NpgsqlException (connection drops, deadlocks)
    ///   - TimeoutException
    /// </summary>
    /// <param name="operation">العملية المطلوب تنفيذها</param>
    /// <param name="logger">logger للتنبيهات</param>
    /// <param name="maxRetries">عدد المحاولات (افتراضي 3)</param>
    /// <param name="opName">اسم العملية للـ logging</param>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        ILogger logger,
        int maxRetries = DefaultMaxRetries,
        string opName = "DB operation",
        CancellationToken ct = default)
    {
        Exception? lastEx = null;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation(attempt, ct);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
            {
                lastEx = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                logger.LogWarning(ex,
                    "{Op}: retry {Attempt}/{Max} after {Delay}s — {Msg}",
                    opName, attempt, maxRetries, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, ct);
            }
        }

        // Last attempt failed — log + rethrow
        logger.LogError(lastEx, "{Op}: failed after {Max} retries", opName, maxRetries);
        throw lastEx!;
    }

    /// <summary>
    /// Overload بدون return value.
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<int, CancellationToken, Task> operation,
        ILogger logger,
        int maxRetries = DefaultMaxRetries,
        string opName = "DB operation",
        CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync<object?>(async (attempt, ct2) =>
        {
            await operation(attempt, ct2);
            return null;
        }, logger, maxRetries, opName, ct);
    }

    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException ||
        ex is TimeoutException ||
        (ex.InnerException != null && IsTransient(ex.InnerException));
}