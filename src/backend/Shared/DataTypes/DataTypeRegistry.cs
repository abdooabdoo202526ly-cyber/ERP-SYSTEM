using System.Text.Json;

namespace ERPSystem.Shared.DataTypes;

/// <summary>
/// DEC-079 PoC: loads all *.json DataType definitions from a directory at startup.
/// Loaded once per app lifecycle. Errors in one file don't block the others.
/// </summary>
public sealed class DataTypeRegistry
{
    private readonly Dictionary<string, DataType> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DataType> _byTable = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = new();

    public IReadOnlyCollection<DataType> All => _byName.Values;
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Load all *.json files from the given directory.
    /// Silent skip on null/empty paths (dev convenience).
    /// </summary>
    public void LoadFromDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
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
                var dt = JsonSerializer.Deserialize<DataType>(json, opts);
                if (dt == null || string.IsNullOrWhiteSpace(dt.Name) || string.IsNullOrWhiteSpace(dt.Table))
                {
                    _errors.Add($"{Path.GetFileName(file)}: missing name/table");
                    continue;
                }
                if (_byName.ContainsKey(dt.Name))
                {
                    _errors.Add($"{Path.GetFileName(file)}: duplicate name '{dt.Name}'");
                    continue;
                }
                _byName[dt.Name] = dt;
                _byTable[dt.Table] = dt;
            }
            catch (Exception ex)
            {
                _errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    public DataType? GetByName(string name) =>
        _byName.TryGetValue(name, out var dt) ? dt : null;

    public DataType? GetByTable(string table) =>
        _byTable.TryGetValue(table, out var dt) ? dt : null;
}