using System.Data;
using Dapper;

namespace ERPSystem.Shared.Infrastructure;

/// <summary>
/// Helpers for soft delete (Sprint-4.5 T-011 / DEC-059).
///
/// Soft delete = mark a row as deleted (deleted_at = now) without removing it.
/// - History preserved (for audit, reports, undo).
/// - All read queries should filter `WHERE deleted_at IS NULL`.
/// </summary>
public static class SoftDeleteHelper
{
    /// <summary>
    /// Soft-delete a row by setting deleted_at = UTC now.
    /// Returns true if the row was updated, false if not found or already deleted.
    /// </summary>
    /// <param name="conn">DB connection (caller manages scope)</param>
    /// <param name="tableName">Table to update (whitelisted — see ValidateTableName)</param>
    /// <param name="idColumn">Primary key column name (usually "id")</param>
    /// <param name="id">Primary key value</param>
    /// <param name="ct">CancellationToken</param>
    public static async Task<bool> SoftDeleteAsync(
        this IDbConnection conn,
        string tableName,
        string idColumn,
        Guid id,
        CancellationToken ct = default)
    {
        ValidateTableName(tableName);
        ValidateColumnName(idColumn);

        var sql = $@"UPDATE {tableName} 
                     SET deleted_at = UTC_TIMESTAMP 
                     WHERE {idColumn} = @Id AND deleted_at IS NULL";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    /// <summary>
    /// Restore a soft-deleted row (set deleted_at = NULL).
    /// </summary>
    public static async Task<bool> RestoreAsync(
        this IDbConnection conn,
        string tableName,
        string idColumn,
        Guid id,
        CancellationToken ct = default)
    {
        ValidateTableName(tableName);
        ValidateColumnName(idColumn);

        var sql = $@"UPDATE {tableName} 
                     SET deleted_at = NULL 
                     WHERE {idColumn} = @Id AND deleted_at IS NOT NULL";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    /// <summary>
    /// Returns SQL fragment: "AND deleted_at IS NULL" or empty string if includeDeleted = true.
    /// Use in SELECT queries to filter out soft-deleted rows by default.
    /// </summary>
    public static string ActiveRecordsFilter(bool includeDeleted = false)
        => includeDeleted ? string.Empty : "AND deleted_at IS NULL";

    // ============ Safety guards (DEC-053 — defense in depth) ============

    private static readonly string[] WhitelistedTables =
    {
        "sales_invoices",
        "projects",
        "customers",
        "vendors",
        "employees",
        "purchase_orders",
        "goods_receipts"
    };

    private static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("tableName is required", nameof(tableName));

        if (!WhitelistedTables.Contains(tableName))
        {
            throw new ArgumentException(
                $"Table '{tableName}' is not in the soft-delete whitelist. " +
                $"Add it to SoftDeleteHelper.WhitelistedTables if it's a soft-deletable entity.",
                nameof(tableName));
        }
    }

    private static void ValidateColumnName(string columnName)
    {
        // Basic SQL injection guard — only allow alphanumeric + underscore
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("columnName is required", nameof(columnName));

        if (!System.Text.RegularExpressions.Regex.IsMatch(columnName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            throw new ArgumentException(
                $"Column name '{columnName}' contains illegal characters. " +
                "Use only alphanumeric + underscore.",
                nameof(columnName));
        }
    }
}