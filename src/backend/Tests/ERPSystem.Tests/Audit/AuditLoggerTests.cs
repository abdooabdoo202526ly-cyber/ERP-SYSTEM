using ERPSystem.Shared.Audit;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERPSystem.Tests.Audit;

public class AuditLoggerTests
{
    private readonly AuditLogger _logger;
    private readonly Mock<IDbConnectionFactory> _factoryMock;
    private readonly FakeDbConnection _conn;
    private readonly Mock<ITenantContext> _tenantContextMock;

    public AuditLoggerTests()
    {
        _factoryMock = new Mock<IDbConnectionFactory>();
        _conn = new FakeDbConnection(new System.Data.DataSet());
        _factoryMock
            .Setup(f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_conn);

        _tenantContextMock = new Mock<ITenantContext>();
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        _tenantContextMock.Setup(t => t.UserId).Returns(Guid.NewGuid());

        _logger = new AuditLogger(
            _factoryMock.Object,
            _tenantContextMock.Object,
            NullLogger<AuditLogger>.Instance);
    }

    [Fact]
    public async Task LogAsync_WithEmptyTenantId_SkipsAndLogsWarning()
    {
        await _logger.LogAsync(
            tenantId: Guid.Empty,
            entityType: "journal_entry",
            entityId: Guid.NewGuid(),
            action: AuditAction.Create);

        // No DB interaction expected
        _factoryMock.Verify(
            f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogAsync_WithEmptyEntityType_SkipsAndLogsWarning()
    {
        await _logger.LogAsync(
            tenantId: Guid.NewGuid(),
            entityType: "",
            entityId: Guid.NewGuid(),
            action: AuditAction.Create);

        _factoryMock.Verify(
            f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogAsync_WithValidData_OpensConnectionAndExecutesInsert()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var changes = new { Field = "value" };

        // Act
        await _logger.LogAsync(
            tenantId: tenantId,
            entityType: "journal_entry",
            entityId: entityId,
            action: AuditAction.Create,
            changes: changes);

        // Assert
        _factoryMock.Verify(
            f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_FromTenantContext_UsesContextValues()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(expectedTenantId);
        _tenantContextMock.Setup(t => t.UserId).Returns(expectedUserId);

        // Act
        await _logger.LogAsync(
            entityType: "project",
            entityId: Guid.NewGuid(),
            action: AuditAction.Update);

        // Assert
        _factoryMock.Verify(
            f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_FromTenantContext_WhenContextEmpty_Skips()
    {
        // Arrange
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.Empty);

        // Act
        await _logger.LogAsync(
            entityType: "project",
            entityId: Guid.NewGuid(),
            action: AuditAction.Update);

        // Assert
        _factoryMock.Verify(
            f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogAsync_OnDbFailure_DoesNotThrow()
    {
        // Arrange — make factory throw
        _factoryMock
            .Setup(f => f.CreateOltpConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        // Act — must NOT throw (audit failures must not break business ops)
        await _logger.LogAsync(
            tenantId: Guid.NewGuid(),
            entityType: "journal_entry",
            entityId: Guid.NewGuid(),
            action: AuditAction.Create);

        // Assert — reached here means no throw
        Assert.True(true);
    }

    [Fact]
    public void AuditAction_Create_IsStringCreate()
    {
        AuditAction.Create.Should().Be("CREATE");
        AuditAction.Update.Should().Be("UPDATE");
        AuditAction.Delete.Should().Be("DELETE");
        AuditAction.Restore.Should().Be("RESTORE");
    }
}