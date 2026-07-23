using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.Events;

/// <summary>
/// Cross-module handler: when an Invoice is created with a ProjectId,
/// log a notification about the cost update (Sprint-4.5 T-010 / DEC-057).
///
/// NOTE: This is an EXAMPLE handler that demonstrates the IDomainEventHandler pattern.
/// The full integration (calling IProjectService.AddActualCostAsync) is planned
/// for Sprint-4.5 T-013 (cross-module integrations).
///
/// Demonstrates cross-module integration WITHOUT the outbox/event-bus overhead —
/// just an in-process handler called synchronously by DomainEventPublisher.
/// </summary>
public class UpdateProjectOnInvoiceHandler : IDomainEventHandler<InvoiceCreatedDomainEvent>
{
    private readonly ILogger<UpdateProjectOnInvoiceHandler> _logger;

    public UpdateProjectOnInvoiceHandler(ILogger<UpdateProjectOnInvoiceHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(InvoiceCreatedDomainEvent @event, CancellationToken ct = default)
    {
        // No-op if invoice has no project
        if (@event.ProjectId == null || @event.ProjectId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "[example] Would update project {ProjectId} actual cost by {Amount} (invoice {InvoiceId})",
            @event.ProjectId, @event.Amount, @event.InvoiceId);

        // Future (Sprint-4.5 T-013): call _projects.AddActualCostAsync(...)
        return Task.CompletedTask;
    }
}