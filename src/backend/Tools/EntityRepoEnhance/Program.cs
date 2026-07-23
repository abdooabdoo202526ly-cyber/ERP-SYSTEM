using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: EntityRepoEnhance <repos-dir>");
    Console.Error.WriteLine("Example: EntityRepoEnhance src/backend/Shared/Generated/Repos/");
    return 1;
}

var reposDir = args[0];
var files = Directory.GetFiles(reposDir, "*Repository.g.cs").OrderBy(f => f).ToList();
Console.WriteLine($"Found {files.Count} repository files in {reposDir}");

int updated = 0, failed = 0;
foreach (var file in files)
{
    try
    {
        var content = File.ReadAllText(file);
        var original = content;

        // Extract entity class name from "public sealed class XxxRepository"
        var classMatch = Regex.Match(content, @"public sealed class (\w+)Repository");
        if (!classMatch.Success)
        {
            Console.Error.WriteLine($"  ✗ {Path.GetFileName(file)}: class name not found");
            failed++;
            continue;
        }
        var entityName = classMatch.Groups[1].Value;

        // Extract columns list from private string Columns => "id, tenant_id, ..."
        var colsMatch = Regex.Match(content, @"private string Columns => ""([^""]+)""");
        if (!colsMatch.Success)
        {
            Console.Error.WriteLine($"  ✗ {Path.GetFileName(file)}: Columns not found");
            failed++;
            continue;
        }
        var columns = colsMatch.Groups[1].Value;
        var colList = columns.Split(", ").ToList();

        // Extract table name from private string Table => "vendors"
        var tableMatch = Regex.Match(content, @"private string Table => ""([^""]+)""");
        var tableName = tableMatch.Success ? tableMatch.Groups[1].Value : entityName.ToLower() + "s";

        // Extract using directive for entity namespace
        var usingMatch = Regex.Match(content, @"using ([\w\.]+);");
        var entityNs = usingMatch.Success ? usingMatch.Groups[1].Value : "Unknown";

        // Build Update method (UPDATE all columns except id/tenant_id/created_at/created_by)
        var updateCols = colList.Where(c =>
            c != "id" && c != "tenant_id" && c != "created_at" && c != "created_by" && c != "deleted_at").ToList();
        var updateSet = string.Join(", ", updateCols.Select(c => $"{c} = @{c}"));
        var updateWhere = "id = @Id AND tenant_id = @TenantId";

        // Build Delete (soft) method
        var softDelete = colList.Contains("deleted_at")
            ? $"UPDATE {tableName} SET deleted_at = NOW(), updated_at = NOW(), updated_by = @UserId WHERE {updateWhere}"
            : $"DELETE FROM {tableName} WHERE {updateWhere}";

        // Build new methods
        var newMethods = new StringBuilder();
        newMethods.AppendLine();
        newMethods.AppendLine($"    public async Task UpdateAsync({entityName} entity, CancellationToken ct)");
        newMethods.AppendLine("    {");
        newMethods.AppendLine("        using var conn = await _db.CreateOltpConnectionAsync(ct);");
        if (colList.Contains("updated_at")) newMethods.AppendLine("        entity.UpdatedAt = DateTime.UtcNow;");
        newMethods.AppendLine($"        await conn.ExecuteAsync(new CommandDefinition(");
        newMethods.AppendLine($"            $\"UPDATE {tableName} SET {updateSet} WHERE {updateWhere}\",");
        newMethods.AppendLine("            entity, cancellationToken: ct));");
        newMethods.AppendLine("    }");
        newMethods.AppendLine();
        newMethods.AppendLine($"    public async Task DeleteAsync(Guid id, Guid tenantId, Guid userId, CancellationToken ct)");
        newMethods.AppendLine("    {");
        newMethods.AppendLine("        using var conn = await _db.CreateOltpConnectionAsync(ct);");
        newMethods.AppendLine($"        await conn.ExecuteAsync(new CommandDefinition(\"{softDelete}\",");
        newMethods.AppendLine("            new { Id = id, TenantId = tenantId, UserId = userId }, cancellationToken: ct));");
        newMethods.AppendLine("    }");
        newMethods.AppendLine();
        newMethods.AppendLine($"    public async Task<int> CountAsync(Guid tenantId, CancellationToken ct)");
        newMethods.AppendLine("    {");
        newMethods.AppendLine("        using var conn = await _db.CreateOltpConnectionAsync(ct);");
        newMethods.AppendLine($"        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(");
        newMethods.AppendLine($"            $\"SELECT COUNT(*) FROM {tableName} WHERE tenant_id = @TenantId\",");
        newMethods.AppendLine("            new { TenantId = tenantId }, cancellationToken: ct));");
        newMethods.AppendLine("    }");

        // Insert new methods before the private Table/Columns/Values definitions
        var insertionPoint = content.IndexOf("private string Table =>");
        if (insertionPoint < 0)
        {
            Console.Error.WriteLine($"  ✗ {Path.GetFileName(file)}: insertion point not found");
            failed++;
            continue;
        }

        content = content.Substring(0, insertionPoint) + newMethods.ToString() + "\n" + content.Substring(insertionPoint);

        File.WriteAllText(file, content);
        updated++;
        Console.WriteLine($"  ✓ {Path.GetFileName(file)}: added Update/Delete/Count");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  ✗ {Path.GetFileName(file)}: {ex.Message}");
        failed++;
    }
}

Console.WriteLine($"\nDone. Updated: {updated}, Failed: {failed}");
return failed > 0 ? 1 : 0;
