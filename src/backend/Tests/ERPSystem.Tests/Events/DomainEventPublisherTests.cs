using ERPSystem.Shared.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERPSystem.Tests.Events;

public class DomainEventPublisherTests
{
    private static IServiceProvider BuildServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IDomainEventPublisher, DomainEventPublisher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_SkipsAndDoesNotCallHandlers()
    {
        var publisher = BuildServiceProvider().GetRequiredService<IDomainEventPublisher>();
        await publisher.PublishAsync<InvoiceCreatedDomainEvent>(null!);
        Assert.True(true);
    }

    [Fact]
    public async Task PublishAsync_NoHandlersRegistered_DoesNotThrow()
    {
        var publisher = BuildServiceProvider().GetRequiredService<IDomainEventPublisher>();
        var evt = new InvoiceCreatedDomainEvent(
            TenantId: Guid.NewGuid(),
            InvoiceId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            Amount: 100m,
            OccurredAt: DateTime.UtcNow);
        await publisher.PublishAsync(evt);
        Assert.True(true);
    }

    [Fact]
    public async Task PublishAsync_WithRegisteredHandler_InvokesHandler()
    {
        var handlerMock = new Mock<IDomainEventHandler<InvoiceCreatedDomainEvent>>();
        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = BuildServiceProvider(s =>
        {
            s.AddSingleton<IDomainEventHandler<InvoiceCreatedDomainEvent>>(handlerMock.Object);
        }).GetRequiredService<IDomainEventPublisher>();

        var evt = new InvoiceCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), null, 50m, DateTime.UtcNow);
        await publisher.PublishAsync(evt);

        handlerMock.Verify(
            h => h.HandleAsync(It.Is<InvoiceCreatedDomainEvent>(e => e == evt), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithMultipleHandlersForSameEvent_InvokesAll()
    {
        var handler1 = new Mock<IDomainEventHandler<InvoiceCreatedDomainEvent>>();
        handler1.Setup(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var handler2 = new Mock<IDomainEventHandler<InvoiceCreatedDomainEvent>>();
        handler2.Setup(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var publisher = BuildServiceProvider(s =>
        {
            s.AddSingleton<IDomainEventHandler<InvoiceCreatedDomainEvent>>(handler1.Object);
            s.AddSingleton<IDomainEventHandler<InvoiceCreatedDomainEvent>>(handler2.Object);
        }).GetRequiredService<IDomainEventPublisher>();

        var evt = new InvoiceCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), null, 10m, DateTime.UtcNow);
        await publisher.PublishAsync(evt);

        handler1.Verify(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        handler2.Verify(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_DoesNotBreakOtherHandlers()
    {
        var failingHandler = new Mock<IDomainEventHandler<InvoiceCreatedDomainEvent>>();
        failingHandler
            .Setup(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler boom"));

        var okHandler = new Mock<IDomainEventHandler<InvoiceCreatedDomainEvent>>();
        okHandler.Setup(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var publisher = BuildServiceProvider(s =>
        {
            s.AddSingleton<IDomainEventHandler<InvoiceCreatedDomainEvent>>(failingHandler.Object);
            s.AddSingleton<IDomainEventHandler<InvoiceCreatedDomainEvent>>(okHandler.Object);
        }).GetRequiredService<IDomainEventPublisher>();

        var evt = new InvoiceCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), null, 10m, DateTime.UtcNow);
        await publisher.PublishAsync(evt);

        okHandler.Verify(h => h.HandleAsync(It.IsAny<InvoiceCreatedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}