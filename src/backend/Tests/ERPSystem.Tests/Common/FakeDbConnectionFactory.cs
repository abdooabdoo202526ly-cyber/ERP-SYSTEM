using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Tests.Common;

/// <summary>
/// In-memory IDbConnectionFactory لمحاكاة Dapper + DataSet بدون DB حقيقي.
/// كل query تستخرج اسم الـ table الأولى في FROM clause وترجع DataTable.
/// للـ tests فقط — لا يستخدم في production.
/// </summary>
public sealed class FakeDbConnectionFactory : IDbConnectionFactory
{
    public DataSet Data { get; } = new();

    public Task<IDbConnection> CreateOltpConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult<IDbConnection>(new FakeDbConnection(Data));

    public Task<IDbConnection> CreateEventStoreConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult<IDbConnection>(new FakeDbConnection(Data));

    public void EnsureTable(string tableName)
    {
        if (!Data.Tables.Contains(tableName))
            Data.Tables.Add(tableName);
    }

    public void AddRow(string tableName, params object[] columns)
    {
        EnsureTable(tableName);
        var table = Data.Tables[tableName]!;
        var row = table.NewRow();
        for (int i = 0; i < columns.Length; i += 2)
        {
            var colName = columns[i].ToString()!;
            if (!table.Columns.Contains(colName))
            {
                var value = columns[i + 1];
                table.Columns.Add(colName, value?.GetType() ?? typeof(object));
            }
            row[colName] = columns[i + 1] ?? DBNull.Value;
        }
        table.Rows.Add(row);
    }

    public int Count(string tableName) =>
        Data.Tables.Contains(tableName) ? Data.Tables[tableName]!.Rows.Count : 0;
}

internal sealed class FakeDbConnection : DbConnection
{
    private readonly DataSet _ds;
    public FakeDbConnection(DataSet ds) => _ds = ds;
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1.0";
    public override ConnectionState State { get; } = ConnectionState.Open;
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }
    protected override DbCommand CreateDbCommand() => new FakeDbCommand(_ds);
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
}

internal sealed class FakeDbCommand : DbCommand
{
    private readonly DataSet _ds;
    private readonly FakeDbParameterCollection _parameters = new();
    public FakeDbCommand(DataSet ds) => _ds = ds;
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new FakeDbParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FakeDbDataReader(_ds, CommandText);
    public override int ExecuteNonQuery() => 0;
    public override object? ExecuteScalar() => null;
}

internal sealed class FakeDbParameter : DbParameter
{
    public override string ParameterName { get; set; } = string.Empty;
    public override object? Value { get; set; }
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
    public override void ResetDbType() { }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _params = new();
    public override int Add(object value) { _params.Add((DbParameter)value); return _params.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) _params.Add((DbParameter)v); }
    public override void Clear() => _params.Clear();
    public override bool Contains(object value) => _params.Contains((DbParameter)value);
    public override int IndexOf(object value) => _params.IndexOf((DbParameter)value);
    public override void Insert(int index, object value) => _params.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _params.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _params.RemoveAt(index);
    public override void RemoveAt(string parameterName) { var i = IndexOf(parameterName); if (i >= 0) _params.RemoveAt(i); }
    protected override DbParameter GetParameter(int index) => _params[index];
    protected override DbParameter GetParameter(string parameterName) => _params[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _params[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _params[IndexOf(parameterName)] = value;
    public override int Count => _params.Count;
    public override object SyncRoot => _params;
    public override int IndexOf(string parameterName) => _params.FindIndex(p => p.ParameterName == parameterName);
    public override bool Contains(string parameterName) => IndexOf(parameterName) >= 0;
    public override void CopyTo(Array array, int index) { for (int i = index; i < array.Length && i - index < _params.Count; i++) array.SetValue(_params[i - index], i); }
    public override System.Collections.IEnumerator GetEnumerator() => _params.GetEnumerator();
}

internal sealed class FakeDbDataReader : DbDataReader
{
    private readonly DataTable? _table;
    private int _rowIndex = -1;

    public FakeDbDataReader(DataSet ds, string sql)
    {
        var tableName = ExtractTableName(sql);
        _table = ds.Tables.Contains(tableName) ? ds.Tables[tableName] : null;
    }

    private static string ExtractTableName(string sql)
    {
        // نمط بسيط: نلتقط أول كلمة بعد FROM أو JOIN
        var m = Regex.Match(sql, @"\b(?:FROM|JOIN)\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "unknown";
    }

    public override object this[int i] => _table!.Rows[_rowIndex][i];
    public override object this[string name] => _table!.Rows[_rowIndex][name];
    public override int Depth => 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => 0;
    public override int FieldCount => _table?.Columns.Count ?? 0;
    public override bool HasRows => _table?.Rows.Count > 0;
    public override bool GetBoolean(int i) => Convert.ToBoolean(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override byte GetByte(int i) => Convert.ToByte(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override long GetBytes(int i, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int i) => Convert.ToChar(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override long GetChars(int i, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override string GetDataTypeName(int i) => _table!.Columns[i].DataType.Name;
    public override DateTime GetDateTime(int i) => Convert.ToDateTime(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int i) => Convert.ToDecimal(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override double GetDouble(int i) => Convert.ToDouble(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override Type GetFieldType(int i) => _table!.Columns[i].DataType;
    public override float GetFloat(int i) => Convert.ToSingle(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override Guid GetGuid(int i) => Guid.Parse(_table!.Rows[_rowIndex][i].ToString()!);
    public override short GetInt16(int i) => Convert.ToInt16(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override int GetInt32(int i) => Convert.ToInt32(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override long GetInt64(int i) => Convert.ToInt64(_table!.Rows[_rowIndex][i], CultureInfo.InvariantCulture);
    public override string GetName(int i) => _table!.Columns[i].ColumnName;
    public override int GetOrdinal(string name) => _table!.Columns.IndexOf(name);
    public override string GetString(int i) => _table!.Rows[_rowIndex][i].ToString()!;
    public override object GetValue(int i) => _table!.Rows[_rowIndex][i];
    public override int GetValues(object[] values)
    {
        var row = _table!.Rows[_rowIndex];
        for (int i = 0; i < values.Length && i < row.ItemArray.Length; i++) values[i] = row[i];
        return Math.Min(values.Length, row.ItemArray.Length);
    }
    public override bool IsDBNull(int i) => _table!.Rows[_rowIndex][i] is DBNull;
    public override bool Read() { _rowIndex++; return _rowIndex < (_table?.Rows.Count ?? 0); }
    public override bool NextResult() => false;
    public override void Close() { }
    public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
}
