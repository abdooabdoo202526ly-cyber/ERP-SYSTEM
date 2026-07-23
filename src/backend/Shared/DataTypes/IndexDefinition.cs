using System.Text.Json.Serialization;

namespace ERPSystem.Shared.DataTypes;

/// <summary>
/// Index metadata for a DataType definition.
/// Example JSON:
///   { "name": "ix_companies_tenant_code", "columns": ["tenant_id", "code"], "unique": true }
/// </summary>
public sealed class IndexDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("columns")]
    public List<string> Columns { get; set; } = new();

    [JsonPropertyName("unique")]
    public bool Unique { get; set; }
}