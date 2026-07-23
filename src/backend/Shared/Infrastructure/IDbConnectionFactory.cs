using System.Data;
using Npgsql;

namespace ERPSystem.Shared.Infrastructure;

/// <summary>
/// مصنع اتصالات قاعدة البيانات
/// يوفّر connection واحد لكل عملية (Scoped) لتجنّب مشاكل الـ connection pooling
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>إنشاء اتصال جديد على قاعدة OLTP (Postgres الرئيسي)</summary>
    Task<IDbConnection> CreateOltpConnectionAsync(CancellationToken ct = default);

    /// <summary>إنشاء اتصال على قاعدة الـ Event Store (MartenDB schema)</summary>
    Task<IDbConnection> CreateEventStoreConnectionAsync(CancellationToken ct = default);
}
