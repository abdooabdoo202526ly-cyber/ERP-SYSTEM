using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Host.Utilities;

/// <summary>
/// مساعد إدخال دفعي (Batch insert) — يقسّم قائمة كبيرة من السجلات إلى دفعات.
/// مفيد عند seeding أو استيراد بيانات ضخمة لتجنّب timeout + DB lock.
///
/// Sprint-4 Day 2 (DEC-023 prevention follow-up, DEC-042).
/// </summary>
public static class BatchInsertHelper
{
    /// <summary>
    /// Default batch size: 1000 records per INSERT.
    /// Calibrated for Postgres + Dapper + typical row size.
    /// </summary>
    public const int DefaultBatchSize = 1000;

    /// <summary>
    /// ينفّذ INSERT على دفعات (batches) ويُسجّل تقدّم العمل في الـ logger.
    /// </summary>
    /// <typeparam name="T">نوع السجلات</typeparam>
    /// <param name="conn">اتصال DB مفتوح</param>
    /// <param name="sql">جملة INSERT (مع parameters تطابق T)</param>
    /// <param name="items">قائمة السجلات</param>
    /// <param name="batchSize">حجم الدفعة الواحدة (افتراضي 1000)</param>
    /// <param name="logger">logger للتقدّم (اختياري)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>إجمالي السجلات المُدخَلة</returns>
    public static async Task<int> BatchInsertAsync<T>(
        this IDbConnection conn,
        string sql,
        IEnumerable<T> items,
        int batchSize = DefaultBatchSize,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "batchSize must be > 0");

        var batches = items.Chunk(batchSize).ToList();
        var totalInserted = 0;

        logger?.LogInformation("BatchInsert: {Batches} batches of up to {BatchSize} records each ({Total} total)",
            batches.Count, batchSize, batches.Sum(b => b.Length));

        for (int i = 0; i < batches.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var batch = batches[i];
            var inserted = await conn.ExecuteAsync(new CommandDefinition(sql, batch, cancellationToken: ct));
            totalInserted += inserted;

            logger?.LogInformation(
                "BatchInsert: batch {Current}/{Total} committed ({Inserted} records)",
                i + 1, batches.Count, inserted);
        }

        logger?.LogInformation("BatchInsert: done — {Total} records inserted in {Batches} batches",
            totalInserted, batches.Count);

        return totalInserted;
    }
}