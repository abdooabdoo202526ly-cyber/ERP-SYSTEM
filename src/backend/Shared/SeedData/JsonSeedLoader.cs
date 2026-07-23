using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// DEC-086: JSON-driven seed data loader.
/// Reads seed files from `data-types/seeds/*.json` and provides typed access
/// to records for the RealisticSeed.
///
/// Schema (per file):
/// {
///   "entity": "Vendor",
///   "table": "vendors",
///   "tenant_id": "f77dbedd-64ff-41ac-b77a-0731183ff744",
///   "records": [
///     { "code": "V-001", "name": "...", ... }
///   ]
/// }
/// </summary>
public sealed class JsonSeedLoader
{
    private readonly Dictionary<string, SeedFile> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SeedFile> Files => _cache;

    public void LoadFromDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(file);
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                var seed = JsonSerializer.Deserialize<SeedFile>(json, opts);
                if (seed == null || string.IsNullOrEmpty(seed.Entity)) continue;
                _cache[seed.Entity] = seed;
            }
            catch
            {
                // Skip invalid JSON — log in future
            }
        }
    }

    public SeedFile? GetFile(string entity) =>
        _cache.TryGetValue(entity, out var f) ? f : null;

    public IEnumerable<Dictionary<string, object>> GetRecords(string entity)
    {
        var file = GetFile(entity);
        if (file == null) yield break;
        foreach (var record in file.Records ?? new List<Dictionary<string, object>>())
        {
            yield return record;
        }
    }
}

public sealed class SeedFile
{
    [JsonPropertyName("entity")]
    public string Entity { get; set; } = string.Empty;

    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; set; }

    [JsonPropertyName("records")]
    public List<Dictionary<string, object>>? Records { get; set; }
}