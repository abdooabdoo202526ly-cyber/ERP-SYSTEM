using System.Text.Json.Serialization;

namespace ERPSystem.Shared.DataTypes;

/// <summary>
/// Foreign key metadata for a field.
/// Example JSON:
///   { "table": "tenants", "column": "id", "on_delete": "cascade" }
/// </summary>
public sealed class ForeignKeyDefinition
{
    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("column")]
    public string Column { get; set; } = "id";

    /// <summary>"cascade" | "set_null" | "restrict" | "no_action" (default)</summary>
    [JsonPropertyName("on_delete")]
    public string OnDelete { get; set; } = "no_action";

    /// <summary>Constraint name (optional). Generated if not provided.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}