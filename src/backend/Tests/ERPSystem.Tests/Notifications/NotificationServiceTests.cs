using ERPSystem.Modules.Notifications.Application.Services;
using ERPSystem.Modules.Notifications.Entities;
using ERPSystem.Modules.Notifications.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Notifications;

public class NotificationServiceTests
{
    private static (NotificationService svc, FakeNotificationRepository repo) Build()
    {
        var repo = new FakeNotificationRepository();
        var svc = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        return (svc, repo);
    }

    [Fact]
    public async Task Create_StoresNotificationWithDefaults()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "LowStock", "Item X below reorder",
            "Quantity 5 < reorder 10", "Item", Guid.NewGuid());

        repo.Items.Should().HaveCount(1);
        var n = repo.Items.Values.First();
        n.TenantId.Should().Be(tenantId);
        n.UserId.Should().Be(userId);
        n.Type.Should().Be("LowStock");
        n.Title.Should().Be("Item X below reorder");
        n.Message.Should().Be("Quantity 5 < reorder 10");
        n.ReferenceType.Should().Be("Item");
        n.ReferenceId.Should().NotBeNull();
        n.IsRead.Should().BeFalse();
        n.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Create_WithoutReference_Allowed()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "Info", "System", "Welcome");

        repo.Items.Should().HaveCount(1);
        var n = repo.Items.Values.First();
        n.ReferenceType.Should().BeNull();
        n.ReferenceId.Should().BeNull();
    }

    [Fact]
    public async Task Create_MultipleForSameUser_AllPersisted()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "A", "T1", "M1");
        await svc.CreateAsync(tenantId, userId, "B", "T2", "M2");
        await svc.CreateAsync(tenantId, userId, "C", "T3", "M3");

        repo.Items.Should().HaveCount(3);
        repo.Items.Values.Select(n => n.Type).Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }

    [Fact]
    public async Task List_ByUser_Tenant_ReturnsOnlyThatUsers()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        await svc.CreateAsync(tenantId, u1, "X", "T", "M");
        await svc.CreateAsync(tenantId, u1, "Y", "T", "M");
        await svc.CreateAsync(tenantId, u2, "Z", "T", "M");

        var r = await svc.ListAsync(tenantId, u1, unreadOnly: false, 0, 50, CancellationToken.None);
        r.Should().HaveCount(2);
        r.Should().OnlyContain(n => n.UserId == u1);
    }

    [Fact]
    public async Task List_FiltersUnreadOnly()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "A", "T", "M");
        await svc.CreateAsync(tenantId, userId, "B", "T", "M");
        var firstId = repo.Items.Values.First().Id;
        repo.Items[firstId].IsRead = true;

        var unread = await svc.ListAsync(tenantId, userId, unreadOnly: true, 0, 50, CancellationToken.None);
        var all = await svc.ListAsync(tenantId, userId, unreadOnly: false, 0, 50, CancellationToken.None);

        unread.Should().HaveCount(1);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_RespectsPagination()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        for (int i = 0; i < 5; i++) await svc.CreateAsync(tenantId, userId, "T", $"T{i}", $"M{i}");

        var firstPage = await svc.ListAsync(tenantId, userId, false, 0, 2, CancellationToken.None);
        var secondPage = await svc.ListAsync(tenantId, userId, false, 2, 2, CancellationToken.None);
        var thirdPage = await svc.ListAsync(tenantId, userId, false, 4, 2, CancellationToken.None);

        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        thirdPage.Should().HaveCount(1);
    }

    [Fact]
    public async Task CountUnread_ReturnsCorrectCount()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "A", "T", "M");
        await svc.CreateAsync(tenantId, userId, "B", "T", "M");
        await svc.CreateAsync(tenantId, userId, "C", "T", "M");
        // mark one as read
        var firstId = repo.Items.Values.First().Id;
        repo.Items[firstId].IsRead = true;

        var count = await svc.CountUnreadAsync(tenantId, userId, CancellationToken.None);
        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkRead_SetsIsReadAndReadAt()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, userId, "T", "title", "msg");
        var n = repo.Items.Values.First();

        await svc.MarkReadAsync(tenantId, userId, n.Id, CancellationToken.None);

        repo.Items[n.Id].IsRead.Should().BeTrue();
        repo.Items[n.Id].ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkRead_WrongTenant_NoOp()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await svc.CreateAsync(tenantId, userId, "T", "title", "msg");
        var n = repo.Items.Values.First();

        await svc.MarkReadAsync(Guid.NewGuid(), userId, n.Id, CancellationToken.None);

        repo.Items[n.Id].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkRead_WrongUser_NoOp()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await svc.CreateAsync(tenantId, userId, "T", "title", "msg");
        var n = repo.Items.Values.First();

        await svc.MarkReadAsync(tenantId, Guid.NewGuid(), n.Id, CancellationToken.None);

        repo.Items[n.Id].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkRead_NotFound_NoOp()
    {
        var (svc, _) = Build();
        // No throw, just no-op
        await svc.MarkReadAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task Create_TakesOutboxOfTakesByUserAndTenant()
    {
        var (svc, repo) = Build();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var u1 = Guid.NewGuid();

        await svc.CreateAsync(t1, u1, "A", "T", "M");
        await svc.CreateAsync(t2, u1, "B", "T", "M");

        // List by user scoped to t1 should only return 1
        var r1 = await svc.ListAsync(t1, u1, false, 0, 50, CancellationToken.None);
        var r2 = await svc.ListAsync(t2, u1, false, 0, 50, CancellationToken.None);

        r1.Should().HaveCount(1);
        r2.Should().HaveCount(1);
    }
}

// ============== Fakes ==============

internal class FakeNotificationRepository : INotificationRepository
{
    public Dictionary<Guid, Notification> Items { get; } = new();
    private readonly object _lock = new();

    public Task InsertAsync(Notification n, CancellationToken ct)
    {
        lock (_lock) { Items[n.Id] = n; }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Notification>> ListAsync(Guid tenantId, Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Notification>>(Items.Values
            .Where(n => n.TenantId == tenantId && n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip).Take(take).ToList());

    public Task<int> CountUnreadAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        Task.FromResult(Items.Values.Count(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead));

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue(id, out var n) ? n : null);

    public Task MarkReadAsync(Guid id, DateTime at, CancellationToken ct)
    {
        if (Items.TryGetValue(id, out var n))
        {
            n.IsRead = true;
            n.ReadAt = at;
        }
        return Task.CompletedTask;
    }
}
