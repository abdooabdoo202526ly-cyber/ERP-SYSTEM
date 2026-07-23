using System.Data;
using Dapper;

namespace ERPSystem.Shared.Infrastructure;

/// <summary>
/// Dapper TypeHandler عام يحول بين enum (كـ string في DB) و string في الكود.
///
/// الـ reason: الـ enums في الـ entities تُخزَّن كنص في DB (status='Draft'),
/// لكن Dapper يحتاج TypeHandler لتحويل string → enum عند القراءة.
///
/// الاستخدام: builder.Services.AddSingleton&lt;EnumStringTypeHandler&gt;();
///
/// مفيد لـ: PayrollRunStatus, GoodsReceiptStatus, PurchaseOrderStatus, VendorBillStatus, LeaveStatus, ...
/// </summary>
public sealed class EnumStringTypeHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override TEnum Parse(object value)
    {
        if (value is null || value is DBNull) return default;
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return default;
        if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var result)) return result;
        throw new InvalidOperationException(
            $"Cannot convert '{s}' to {typeof(TEnum).Name}. Valid: {string.Join(", ", Enum.GetNames<TEnum>())}");
    }
}
