using Dapper;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Companies.Infrastructure;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;

namespace ERPSystem.Tests.Procurement;

// ============== Shared Fakes for Procurement tests ==============
// نمط موحّد مع باقي ملفات الاختبار (Projects/ProjectServiceTests.cs):
// كل service له Fake Repository في الـ DI. الـ fakes هنا مشتركة بين 4 ملفات اختبار.

internal class FakeVendorRepository : IVendorRepository
{
    public Dictionary<Guid, Vendor> Items { get; } = new();
    private readonly object _lock = new();

    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue(id, out var v) ? v : null);

    public Task<Vendor?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(v => v.TenantId == tenantId && v.Code == code));

    public Task<IReadOnlyList<Vendor>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Vendor>>(Items.Values
            .Where(v => v.TenantId == tenantId && (includeInactive || v.IsActive))
            .ToList());

    public Task<IReadOnlyList<Vendor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Vendor>>(Items.Values.Where(v => ids.Contains(v.Id)).ToList());

    public Task InsertAsync(Vendor vendor, CancellationToken ct)
    {
        lock (_lock) { Items[vendor.Id] = vendor; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Vendor vendor, CancellationToken ct)
    {
        lock (_lock) { Items[vendor.Id] = vendor; }
        return Task.CompletedTask;
    }
}

internal class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public Dictionary<Guid, PurchaseOrder> Items { get; } = new();
    public List<PurchaseOrderLine> Lines { get; } = new();
    private readonly object _lock = new();

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var po = Items.TryGetValue(id, out var p) ? p : null;
        if (po != null)
        {
            po.Lines = Lines.Where(l => l.PurchaseOrderId == po.Id).ToList();
        }
        return Task.FromResult(po);
    }

    public Task<PurchaseOrder?> GetByPoNumberAsync(Guid tenantId, string poNumber, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(p => p.TenantId == tenantId && p.PoNumber == poNumber));

    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(Guid tenantId, Guid? vendorId, PurchaseOrderStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Items.Values
            .Where(p => p.TenantId == tenantId
                && (vendorId == null || p.VendorId == vendorId)
                && (status == null || p.Status == status))
            .ToList());

    public Task InsertAsync(PurchaseOrder po, CancellationToken ct)
    {
        lock (_lock) { Items[po.Id] = po; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PurchaseOrder po, CancellationToken ct)
    {
        lock (_lock) { Items[po.Id] = po; }
        return Task.CompletedTask;
    }

    public Task InsertLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct)
    {
        // Real repo sets PurchaseOrderId via SQL — mimic here
        foreach (var l in lines) l.PurchaseOrderId = poId;
        lock (_lock) { Lines.AddRange(lines); }
        return Task.CompletedTask;
    }

    public Task UpdateLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct)
    {
        lock (_lock)
        {
            Lines.RemoveAll(l => l.PurchaseOrderId == poId);
            Lines.AddRange(lines);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(Guid poId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrderLine>>(Lines.Where(l => l.PurchaseOrderId == poId).ToList());

    public Task<IReadOnlyList<PurchaseOrder>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Items.Values.Where(p => ids.Contains(p.Id)).ToList());
}

internal class FakeDocumentSequenceRepository : IDocumentSequenceRepository
{
    private readonly Dictionary<string, int> _counters = new();

    public Task<string> GetNextNumberAsync(Guid tenantId, string prefix, CancellationToken ct)
    {
        lock (_counters)
        {
            var key = $"{tenantId:N}-{prefix}";
            _counters.TryGetValue(key, out var n);
            n++;
            _counters[key] = n;
            return Task.FromResult($"{prefix}-2026-{n:D4}");
        }
    }
}

internal class FakeGoodsReceiptRepository : IGoodsReceiptRepository
{
    public Dictionary<Guid, GoodsReceipt> Items { get; } = new();
    public List<GoodsReceiptLine> Lines { get; } = new();
    private readonly object _lock = new();

    public Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var gr = Items.TryGetValue(id, out var g) ? g : null;
        if (gr != null) gr.Lines = Lines.Where(l => l.GoodsReceiptId == gr.Id).ToList();
        return Task.FromResult(gr);
    }

    public Task<GoodsReceipt?> GetByGrNumberAsync(Guid tenantId, string grNumber, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(g => g.TenantId == tenantId && g.GrNumber == grNumber));

    public Task<IReadOnlyList<GoodsReceipt>> ListAsync(Guid tenantId, Guid? poId, GoodsReceiptStatus? status, int skip, int take, CancellationToken ct)
    {
        var list = Items.Values
            .Where(g => g.TenantId == tenantId
                && (poId == null || g.PurchaseOrderId == poId)
                && (status == null || g.Status == status))
            .ToList();
        foreach (var g in list) g.Lines = Lines.Where(l => l.GoodsReceiptId == g.Id).ToList();
        return Task.FromResult<IReadOnlyList<GoodsReceipt>>(list);
    }

    public Task InsertAsync(GoodsReceipt gr, CancellationToken ct)
    {
        lock (_lock) { Items[gr.Id] = gr; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(GoodsReceipt gr, CancellationToken ct)
    {
        lock (_lock) { Items[gr.Id] = gr; }
        return Task.CompletedTask;
    }

    public Task InsertLinesAsync(Guid tenantId, Guid grId, IEnumerable<GoodsReceiptLine> lines, CancellationToken ct)
    {
        // Real repo sets GoodsReceiptId via SQL — the fake mimics that by setting it here
        // (because the service doesn't pre-populate GoodsReceiptId on each line).
        foreach (var l in lines) l.GoodsReceiptId = grId;
        lock (_lock) { Lines.AddRange(lines); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GoodsReceiptLine>> GetLinesAsync(Guid grId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GoodsReceiptLine>>(Lines.Where(l => l.GoodsReceiptId == grId).ToList());
}

internal class FakeCompanyRepository : ICompanyRepository
{
    public Dictionary<Guid, Company> Companies { get; } = new();
    public Guid? HoldingCompanyId { get; set; }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Companies.TryGetValue(id, out var c) ? c : null);
    public Task<Company?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Companies.Values.FirstOrDefault(c => c.TenantId == tenantId && c.Code == code));
    public Task<IReadOnlyList<Company>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies.Values.Where(c => c.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Company>> ListSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies.Values.Where(c => c.ParentCompanyId == parentCompanyId).ToList());
    public Task<Guid?> GetHoldingCompanyIdAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(HoldingCompanyId);
    public Task InsertAsync(Company company, CancellationToken ct) { Companies[company.Id] = company; return Task.CompletedTask; }
    public Task UpdateAsync(Company company, CancellationToken ct) { Companies[company.Id] = company; return Task.CompletedTask; }
}

internal class FakeWarehouseRepository : IWarehouseRepository
{
    public Dictionary<Guid, Warehouse> Items { get; } = new();

    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue(id, out var w) ? w : null);
    public Task<Warehouse?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(w => w.TenantId == tenantId && w.Code == code));
    public Task<IReadOnlyList<Warehouse>> ListAsync(Guid tenantId, Guid? companyId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Warehouse>>(Items.Values.Where(w => w.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Warehouse>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Warehouse>>(Items.Values.Where(w => ids.Contains(w.Id)).ToList());
    public Task InsertAsync(Warehouse warehouse, CancellationToken ct) { Items[warehouse.Id] = warehouse; return Task.CompletedTask; }
    public Task UpdateAsync(Warehouse warehouse, CancellationToken ct) { Items[warehouse.Id] = warehouse; return Task.CompletedTask; }
}

internal class FakeStockMovementService : IStockMovementService
{
    public List<ReceiveStockRequest> CreatedReceives { get; } = new();
    public List<Guid> PostedMovements { get; } = new();
    public bool FailOnPost { get; set; }

    public Task<StockMovementResult<StockMovementResponse>> CreateReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct)
    {
        CreatedReceives.Add(req);
        var resp = new StockMovementResponse
        {
            Id = Guid.NewGuid(), Reference = req.Reference, Type = StockMovementType.Receive,
            Status = StockMovementStatus.Draft, MovementDate = req.MovementDate,
            ItemId = req.ItemId, WarehouseId = req.WarehouseId, Quantity = req.Quantity, UnitCost = req.UnitCost,
            CreatedAt = DateTime.UtcNow
        };
        return Task.FromResult(StockMovementResult<StockMovementResponse>.Ok(resp));
    }

    public Task<StockMovementResult<StockMovementResponse>> PostAsync(Guid tenantId, Guid userId, Guid movementId, CancellationToken ct)
    {
        if (FailOnPost) return Task.FromResult(StockMovementResult<StockMovementResponse>.Fail("simulated post failure", StockErrorCode.PostFailed));
        PostedMovements.Add(movementId);
        return Task.FromResult(StockMovementResult<StockMovementResponse>.Ok(new StockMovementResponse
        {
            Id = movementId, Type = StockMovementType.Receive, Status = StockMovementStatus.Posted,
            MovementDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        }));
    }

    // باقي الـ methods غير مستخدمة في اختبارات Procurement
    public Task<StockMovementResult<StockMovementResponse>> CreateIssueAsync(Guid tenantId, Guid userId, IssueStockRequest req, CancellationToken ct) => throw new NotImplementedException();
    public Task<StockMovementResult<StockMovementResponse>> CreateTransferAsync(Guid tenantId, Guid userId, TransferStockRequest req, CancellationToken ct) => throw new NotImplementedException();
    public Task<StockMovementResult<StockMovementResponse>> CreateAdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct) => throw new NotImplementedException();
    public Task<StockMovementResult<StockMovementResponse>> ReverseAsync(Guid tenantId, Guid userId, Guid movementId, string? reason, CancellationToken ct) => throw new NotImplementedException();
    public Task<StockMovementResult<StockMovementResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task<StockMovementResult<IReadOnlyList<StockMovementResponse>>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct) => throw new NotImplementedException();
}

internal class FakeVendorBillRepository : IVendorBillRepository
{
    public Dictionary<Guid, VendorBill> Items { get; } = new();
    public List<VendorBillLine> Lines { get; } = new();
    private readonly object _lock = new();

    public Task<VendorBill?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var b = Items.TryGetValue(id, out var found) ? found : null;
        if (b != null) b.Lines = Lines.Where(l => l.VendorBillId == b.Id).ToList();
        return Task.FromResult(b);
    }

    public Task<VendorBill?> GetByBillNumberAsync(Guid tenantId, string billNumber, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(b => b.TenantId == tenantId && b.BillNumber == billNumber));

    public Task<IReadOnlyList<VendorBill>> ListAsync(Guid tenantId, Guid? vendorId, Guid? grId, VendorBillStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VendorBill>>(Items.Values
            .Where(b => b.TenantId == tenantId
                && (vendorId == null || b.VendorId == vendorId)
                && (grId == null || b.GoodsReceiptId == grId)
                && (status == null || b.Status == status))
            .ToList());

    public Task InsertAsync(VendorBill bill, CancellationToken ct)
    {
        lock (_lock) { Items[bill.Id] = bill; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VendorBill bill, CancellationToken ct)
    {
        lock (_lock) { Items[bill.Id] = bill; }
        return Task.CompletedTask;
    }

    public Task InsertLinesAsync(Guid tenantId, Guid billId, IEnumerable<VendorBillLine> lines, CancellationToken ct)
    {
        // Real repo sets VendorBillId via SQL — mimic here
        foreach (var l in lines) l.VendorBillId = billId;
        lock (_lock) { Lines.AddRange(lines); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VendorBillLine>> GetLinesAsync(Guid billId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VendorBillLine>>(Lines.Where(l => l.VendorBillId == billId).ToList());
}

internal class FakeJournalEntryService : IJournalEntryService
{
    public List<JournalEntryResponse> CreatedEntries { get; } = new();
    public List<Guid> PostedEntries { get; } = new();
    public bool FailNextCreate { get; set; }
    public bool FailNextPost { get; set; }

    public Task<FinanceResult<JournalEntryResponse>> CreateDraftAsync(Guid tenantId, Guid userId, PostJournalEntryRequest request, CancellationToken ct)
    {
        if (FailNextCreate) return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail("simulated JE create failure", FinanceErrorCode.ValidationError));
        var resp = new JournalEntryResponse
        {
            Id = Guid.NewGuid(), EntryNumber = "JE-2026-0001",
            EntryDate = request.EntryDate, Description = request.Description, Reference = request.Reference,
            Status = JournalEntryStatus.Draft,
            Lines = request.Lines.Select((l, i) => new JournalLineResponse
            {
                LineNumber = i + 1, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Description = l.Description
            }).ToList()
        };
        resp.TotalDebit = request.Lines.Sum(l => l.Debit);
        resp.TotalCredit = request.Lines.Sum(l => l.Credit);
        CreatedEntries.Add(resp);
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(resp));
    }

    public Task<FinanceResult<JournalEntryResponse>> PostAsync(Guid tenantId, Guid userId, Guid entryId, CancellationToken ct)
    {
        if (FailNextPost) return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail("simulated post failure", FinanceErrorCode.ValidationError));
        var e = CreatedEntries.FirstOrDefault(j => j.Id == entryId);
        if (e != null) e.Status = JournalEntryStatus.Posted;
        PostedEntries.Add(entryId);
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(e ?? new JournalEntryResponse { Id = entryId, Status = JournalEntryStatus.Posted }));
    }

    public Task<FinanceResult<JournalEntryResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(CreatedEntries.FirstOrDefault(j => j.Id == id) ?? new JournalEntryResponse { Id = id }));
    public Task<FinanceResult<IReadOnlyList<JournalEntryResponse>>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult(FinanceResult<IReadOnlyList<JournalEntryResponse>>.Ok(CreatedEntries));
}

/// <summary>
/// IDbConnectionFactory يكشف فقط عن accounts المخزنة مسبقاً،
/// لتجاوز Dapper tuple-mapping في الـ FakeDbConnectionFactory العام.
/// يستخدم في VendorBillService الذي يبحث عن accounts 1240/2210.
/// </summary>
internal class FakeAccountsConnectionFactory : ERPSystem.Shared.Infrastructure.IDbConnectionFactory
{
    public Dictionary<Guid, Guid> InventoryAccountIdByTenant { get; } = new();
    public Dictionary<Guid, Guid> ApAccountIdByTenant { get; } = new();

    public Task<System.Data.IDbConnection> CreateOltpConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult<System.Data.IDbConnection>(new AccountsConnection(InventoryAccountIdByTenant, ApAccountIdByTenant));

    public Task<System.Data.IDbConnection> CreateEventStoreConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult<System.Data.IDbConnection>(new AccountsConnection(InventoryAccountIdByTenant, ApAccountIdByTenant));
}

internal class AccountsConnection : System.Data.Common.DbConnection
{
    private readonly Dictionary<Guid, Guid> _inv;
    private readonly Dictionary<Guid, Guid> _ap;
    public AccountsConnection(Dictionary<Guid, Guid> inv, Dictionary<Guid, Guid> ap) { _inv = inv; _ap = ap; }
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1.0";
    public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }
    protected override System.Data.Common.DbCommand CreateDbCommand() => new AccountsCommand(_inv, _ap);
    protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();
}

internal class AccountsCommand : System.Data.Common.DbCommand
{
    private readonly Dictionary<Guid, Guid> _inv;
    private readonly Dictionary<Guid, Guid> _ap;
    public AccountsCommand(Dictionary<Guid, Guid> inv, Dictionary<Guid, Guid> ap) { _inv = inv; _ap = ap; }
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; } = 30;
    public override System.Data.CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
    protected override System.Data.Common.DbConnection? DbConnection { get; set; }
    protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } = new EmptyParamCollection();
    protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    public override void Prepare() { }
    protected override System.Data.Common.DbParameter CreateDbParameter() => new StubDbParameter();
    protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) =>
        new AccountsReader(CommandText, _inv, _ap);
    public override int ExecuteNonQuery() => 0;
    public override object? ExecuteScalar() => null;
}

internal class EmptyParamCollection : System.Data.Common.DbParameterCollection
{
    public override int Add(object value) => 0;
    public override void AddRange(Array values) { }
    public override void Clear() { }
    public override bool Contains(object value) => false;
    public override int IndexOf(object value) => -1;
    public override void Insert(int index, object value) { }
    public override void Remove(object value) { }
    public override void RemoveAt(int index) { }
    public override void RemoveAt(string parameterName) { }
    protected override System.Data.Common.DbParameter GetParameter(int index) => throw new NotSupportedException();
    protected override System.Data.Common.DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
    protected override void SetParameter(int index, System.Data.Common.DbParameter value) { }
    protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value) { }
    public override int Count => 0;
    public override object SyncRoot => this;
    public override int IndexOf(string parameterName) => -1;
    public override bool Contains(string parameterName) => false;
    public override void CopyTo(Array array, int index) { }
    public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
}

internal class StubDbParameter : System.Data.Common.DbParameter
{
    public override string ParameterName { get; set; } = string.Empty;
    public override object? Value { get; set; }
    public override System.Data.DbType DbType { get; set; }
    public override System.Data.ParameterDirection Direction { get; set; } = System.Data.ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override System.Data.DataRowVersion SourceVersion { get; set; } = System.Data.DataRowVersion.Current;
    public override void ResetDbType() { }
}

internal class AccountsReader : System.Data.Common.DbDataReader
{
    private readonly List<(string Code, Guid AcctId)> _rows;
    private int _idx = -1;
    public AccountsReader(string sql, Dictionary<Guid, Guid> inv, Dictionary<Guid, Guid> ap)
    {
        _rows = new List<(string Code, Guid AcctId)>();
        foreach (var kv in inv) _rows.Add(("1240", kv.Value));
        foreach (var kv in ap) _rows.Add(("2210", kv.Value));
    }
    public override object this[int i] => i == 0 ? (object)_rows[_idx].Code : _rows[_idx].AcctId;
    public override object this[string name] => name.Equals("code", StringComparison.OrdinalIgnoreCase) ? (object)_rows[_idx].Code : _rows[_idx].AcctId;
    public override int Depth => 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => 0;
    public override int FieldCount => 2;
    public override bool HasRows => _rows.Count > 0;
    public override bool GetBoolean(int i) => throw new NotSupportedException();
    public override byte GetByte(int i) => throw new NotSupportedException();
    public override long GetBytes(int i, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int i) => throw new NotSupportedException();
    public override long GetChars(int i, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override string GetDataTypeName(int i) => i == 0 ? "varchar" : "uuid";
    public override DateTime GetDateTime(int i) => throw new NotSupportedException();
    public override decimal GetDecimal(int i) => throw new NotSupportedException();
    public override double GetDouble(int i) => throw new NotSupportedException();
    public override Type GetFieldType(int i) => i == 0 ? typeof(string) : typeof(Guid);
    public override float GetFloat(int i) => throw new NotSupportedException();
    public override Guid GetGuid(int i) => _rows[_idx].AcctId;
    public override short GetInt16(int i) => throw new NotSupportedException();
    public override int GetInt32(int i) => throw new NotSupportedException();
    public override long GetInt64(int i) => throw new NotSupportedException();
    public override string GetName(int i) => i == 0 ? "code" : "id";
    public override int GetOrdinal(string name) => name.Equals("code", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    public override string GetString(int i) => _rows[_idx].Code;
    public override object GetValue(int i) => i == 0 ? (object)_rows[_idx].Code : _rows[_idx].AcctId;
    public override int GetValues(object[] values)
    {
        values[0] = _rows[_idx].Code;
        values[1] = _rows[_idx].AcctId;
        return 2;
    }
    public override bool IsDBNull(int i) => false;
    public override bool Read() { _idx++; return _idx < _rows.Count; }
    public override bool NextResult() => false;
    public override void Close() { }
    public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
}
