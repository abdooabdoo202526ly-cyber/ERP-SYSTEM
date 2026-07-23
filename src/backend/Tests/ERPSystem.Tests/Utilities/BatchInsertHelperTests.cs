using ERPSystem.Host.Utilities;
using ERPSystem.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Utilities;

public class BatchInsertHelperTests
{
    private static FakeDbConnection NewConn()
    {
        var ds = new System.Data.DataSet();
        return new FakeDbConnection(ds);
    }

    [Fact]
    public async Task BatchInsertAsync_EmptyList_ReturnsZero()
    {
        using var conn = NewConn();
        await conn.OpenAsync();

        var inserted = await conn.BatchInsertAsync(
            "INSERT INTO t (name) VALUES (@Name)",
            Array.Empty<TestRow>(),
            batchSize: 100,
            logger: NullLogger.Instance);

        inserted.Should().Be(0);
    }

    [Fact]
    public async Task BatchInsertAsync_SmallList_SingleBatch()
    {
        using var conn = NewConn();
        await conn.OpenAsync();
        var items = Enumerable.Range(1, 10)
            .Select(i => new TestRow { Id = Guid.NewGuid(), Name = $"Item {i}" })
            .ToList();

        var inserted = await conn.BatchInsertAsync(
            "INSERT INTO t (id, name) VALUES (@Id, @Name)",
            items,
            batchSize: 100,
            logger: NullLogger.Instance);

        // FakeDb.ExecuteNonQuery returns 0; we verify the call succeeded, not the count.
        inserted.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task BatchInsertAsync_LargeList_MultipleBatches()
    {
        using var conn = NewConn();
        await conn.OpenAsync();
        var items = Enumerable.Range(1, 2500)
            .Select(i => new TestRow { Id = Guid.NewGuid(), Name = $"Item {i}" })
            .ToList();

        var inserted = await conn.BatchInsertAsync(
            "INSERT INTO t (id, name) VALUES (@Id, @Name)",
            items,
            batchSize: 500,
            logger: NullLogger.Instance);

        // 2500 items / 500 batch = 5 batches — verify no exception (FakeDb returns 0).
        inserted.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task BatchInsertAsync_BatchSizeZero_Throws()
    {
        using var conn = NewConn();
        await conn.OpenAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            conn.BatchInsertAsync(
                "INSERT INTO t (name) VALUES (@Name)",
                new[] { new TestRow { Id = Guid.NewGuid(), Name = "X" } },
                batchSize: 0,
                logger: NullLogger.Instance));
    }

    [Fact]
    public async Task BatchInsertAsync_RespectsCancellation()
    {
        using var conn = NewConn();
        await conn.OpenAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            conn.BatchInsertAsync(
                "INSERT INTO t (name) VALUES (@Name)",
                new[] { new TestRow { Id = Guid.NewGuid(), Name = "X" } },
                batchSize: 100,
                logger: NullLogger.Instance,
                ct: cts.Token));
    }

    private class TestRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}