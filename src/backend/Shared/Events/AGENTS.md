# 🚌 src/backend/Shared/Events/AGENTS.md

> Event Bus (Outbox Pattern) — ✅ Phase 2.4 (cross-module integration).
>
> محدّث: 2026-06-24 — إضافة Phase 3+ context

## شو فيه

```
Shared/Events/
├── StockEvents.cs            # IIntegrationEvent + 3 records
├── Infrastructure/
│   ├── OutboxEvent.cs        # row in outbox_events
│   └── OutboxRepository.cs   # InsertAsync, FetchUnprocessed, MarkProcessed/Failed, ResetForRetry
└── Application/Services/
    ├── EventBus.cs              # PublishAsync<T>(writes to outbox)
    ├── EventHandlers.cs        # IProcessedEventsRepository + IIntegrationEventHandler<T>
    └── OutboxProcessorHostedService.cs   # background, every 5s
```

## Outbox Pattern (The Core Idea)

```
User: POST /api/inventory/movements/{id}/post
    ↓
[StockMovementService.PostAsync]
    ├─ Update movement status (transaction)
    ├─ UPSERT stock_level (transaction)
    ├─ INSERT notification if low (transaction)
    ├─ ⭐ INSERT outbox_event (transaction) — atomic!
    └─ COMMIT
    ↓
[Response: 200 OK] (fast, no waiting)

[Background: OutboxProcessor (every 5s)]
    ↓
    ├─ SELECT unprocessed events
    ├─ For each event:
    │   ├─ Check processed_events (idempotency by EventId)
    │   ├─ Find handler by type (via DI)
    │   ├─ Execute handler: PostingRulesService → JournalEntry
    │   ├─ Mark processed (processed_events + processed_at)
    │   └─ On failure: increment retry, log
    ↓
[Finance: new JournalEntry in DB]
```

**Atomicity guarantee**: the event is persisted in the SAME transaction as the
business operation. If the transaction rolls back, no event is published.
This solves the "dual-write" problem (write to DB + write to message bus).

## Idempotency

- Every event has `EventId` (UUID)
- `processed_events` table dedupes by EventId
- If processor sees EventId already processed → marks outbox row processed and skips
- This means **at-least-once delivery** with safe retry semantics

## Integration Events (Phase 2.4)

| Event | Producer | Consumer (Phase 2.4) | Effect |
|-------|----------|---------------------|--------|
| StockReceivedEvent | StockMovementService.PostAsync (Receive) | StockReceivedEventHandler (Finance) | ApplyRulesAsync("StockReceived") → JournalEntry Dr Inventory / Cr A/P |
| StockIssuedEvent | StockMovementService.PostAsync (Issue) | StockIssuedEventHandler (Finance) | ApplyRulesAsync("StockIssued") → JournalEntry Dr COGS / Cr Inventory |
| JournalEntryPostedEvent | (future, PR #7+) | — | placeholder for notifications |

**Future events** (PR #8+): BudgetExceeded, ProjectMaterialRequested, etc.

## Handlers Pattern

```csharp
public interface IIntegrationEventHandler<in T> where T : IIntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken ct);
}
```

Handlers are **Scoped** (resolved per event from a DI scope created in the processor).
Auto-discovered via DI — just register `AddScoped<IIntegrationEventHandler<TEvent>, THandler>()`.

## Retry Strategy

- `MaxRetries = 3` (default, overridable per event)
- On failure: `retry_count++` + `last_error` recorded
- When `retry_count >= max_retries`: marked processed (gives up — admin can manual-retry via `/api/events/retry/{id}`)
- Production-grade: would use `FOR UPDATE SKIP LOCKED` for multi-instance safety. Phase 2.4 keeps it simple (single-instance).

## Endpoints (4)

| Method | Path | الـ Function |
|--------|------|-------------|
| GET | /api/events/outbox | list pending events for tenant |
| GET | /api/events/processed | list processed (audit trail) |
| GET | /api/events/pending-count | count only |
| POST | /api/events/retry/{id} | admin manual retry (resets retry_count + clears last_error) |

## لما تشتغل هنا

- إضافة event جديد: عرّف record implementing IIntegrationEvent، عرّف handler (IIntegrationEventHandler<T>)، DI register handler، publish في الـ business logic
- إضافة channel (Kafka, RabbitMQ, Service Bus): استبدل OutboxProcessor بسياق يستهلك `IEventBus.PublishAsync` + broker
- Multi-instance: حوّل الـ processor ليستخدم `FOR UPDATE SKIP LOCKED` على outbox_events
- تحسين idempotency: ضيف dedup على processed_events حتى لو expiry (مثلاً 30 يوم)

## بعد التعديل

- شغّل `dotnet test` (8 tests جديد + 84 سابق = 92/92)
- إذا أضفت event: اكتب handler test + business logic test

## تكامل مع الموديولات الأخرى

- **Inventory** (Phase 2.3): publishes StockReceived/StockIssued on PostAsync
- **Finance** (Phase 1): PostingRulesService → JournalEntry (the main integration)
- **Notifications** (Phase 2.3): can also consume events for alerts (PR #7+)
- **Projects** (Phase 2.1): BudgetExceeded events (مستقبلي)

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../../Modules/Finance/AGENTS.md`](../../Modules/Finance/AGENTS.md) — PostingRulesService
- [`../../Modules/Inventory/AGENTS.md`](../../Modules/Inventory/AGENTS.md) — StockMovementService publishes
- [`../../Modules/Procurement/AGENTS.md`](../../Modules/Procurement/AGENTS.md) — Phase 3 (POApproved, GoodsReceived)
- [`../../Modules/HR/AGENTS.md`](../../Modules/HR/AGENTS.md) — Phase 3.5 (LeaveApproved)
- [`../../Modules/Payroll/AGENTS.md`](../../Modules/Payroll/AGENTS.md) — Phase 4 (PayrollPosted → Finance auto-post)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
