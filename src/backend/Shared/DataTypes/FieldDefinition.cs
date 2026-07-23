using System.Text.Json.Serialization;

namespace ERPSystem.Shared.DataTypes;

/// <summary>
/// Column metadata for a JSON DataType definition.
/// Example JSON:
///   { "name": "code", "type": "varchar(20)", "nullable": false, "primary_key": false }
/// </summary>
public sealed class FieldDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;  // e.g. "uuid", "varchar(20)", "timestamptz", "numeric(18,4)"

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; } = true;

    [JsonPropertyName("primary_key")]
    public bool PrimaryKey { get; set; }

    [JsonPropertyName("default")]
    public string? Default { get; set; }  // raw SQL expression, e.g. "now()", "'LYD'", "false"

    [JsonPropertyName("foreign_key")]
    public ForeignKeyDefinition? ForeignKey { get; set; }
}