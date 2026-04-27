# Outbox Pattern

The Outbox Pattern guarantees **eventual consistency** between application state and event publication to external systems. Business state and events are persisted in the same transaction; a background service publishes pending events asynchronously.

> **Note:** Implementation examples use the `NH` prefix for NHibernate classes (e.g., `NHDomainEventRepository`). Adapt names to your project's conventions.

## Core Concept

```
WITHOUT OUTBOX:
  1. Save to Database  --> Success
  2. Publish to Bus    --> Failure (network error)
  Result: State saved but event lost

WITH OUTBOX:
  1. BEGIN TRANSACTION
     - Save Business State  --> Success
     - Save Event to Outbox --> Success
  2. COMMIT TRANSACTION     --> Atomic
  3. Background Job (async)
     - Publish pending events --> Retry until success
  Result: State and event always consistent (at-least-once delivery)
```

## File Structure

```
src/
├── {project}.domain/
│   ├── entities/
│   │   └── DomainEvent.cs                <- Event entity
│   ├── events/
│   │   ├── PublishableEventAttribute.cs   <- Marks publishable events
│   │   └── orders/
│   │       └── OrderCreatedEvent.cs
│   └── interfaces/
│       ├── IEventStore.cs                <- High-level interface
│       └── repositories/
│           └── IDomainEventRepository.cs  <- Repository interface
│
├── {project}.infrastructure/
│   └── nhibernate/
│       ├── EventStore.cs                      <- IEventStore impl
│       ├── {Prefix}DomainEventRepository.cs   <- Repository impl
│       └── mappers/
│           └── DomainEventMapper.cs
│
└── {project}.migrations/
    └── M00XCreateDomainEventsTable.cs
```

## DomainEvent Entity

```csharp
namespace {ProjectName}.domain.entities;

/// <summary>
/// Represents a domain event stored for auditing and outbox pattern purposes.
/// </summary>
public class DomainEvent
{
    // Identification
    public virtual Guid Id { get; set; }
    public virtual Guid OrganizationId { get; set; }

    // Aggregate info
    public virtual string AggregateType { get; set; } = string.Empty;
    public virtual Guid AggregateId { get; set; }

    // Event data
    public virtual string EventType { get; set; } = string.Empty;
    public virtual string EventData { get; set; } = string.Empty;
    public virtual DateTime OccurredAt { get; set; }

    // Audit context
    public virtual Guid? UserId { get; set; }
    public virtual string? UserName { get; set; }
    public virtual string? IpAddress { get; set; }
    public virtual string? CorrelationId { get; set; }
    public virtual Guid? ConversationId { get; set; }

    // Outbox control
    public virtual bool ShouldPublish { get; set; }
    public virtual DateTime? PublishedAt { get; set; }
    public virtual int PublishAttempts { get; set; }
    public virtual string? LastPublishError { get; set; }
    public virtual int Version { get; set; } = 1;
}
```

Key outbox properties:

| Property | Purpose |
|----------|---------|
| `ShouldPublish` | `true` if event has `[PublishableEvent]`, set automatically |
| `PublishedAt` | `null` = pending, dated = published |
| `PublishAttempts` | Retry counter (max 3) |
| `LastPublishError` | Last error for debugging |

## PublishableEvent Attribute

```csharp
namespace {ProjectName}.domain.events;

/// <summary>
/// Marks a domain event as publishable to an external message bus.
/// Events without this attribute are only stored for auditing purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PublishableEventAttribute : Attribute
{
}
```

## IEventStore Interface

```csharp
namespace {ProjectName}.domain.interfaces;

/// <summary>
/// High-level interface for appending and retrieving domain events.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends a domain event to the event store.
    /// Automatically detects if event should be published based on [PublishableEvent] attribute.
    /// </summary>
    Task AppendAsync<TEvent>(
        TEvent @event,
        Guid organizationId,
        string aggregateType,
        Guid aggregateId,
        Guid? userId = null,
        string? userName = null,
        string? ipAddress = null,
        string? correlationId = null) where TEvent : class;

    Task<IList<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken ct);
    Task<IList<DomainEvent>> GetEventsByOrganizationAsync(Guid organizationId, CancellationToken ct);
}
```

## IDomainEventRepository Interface

```csharp
namespace {ProjectName}.domain.interfaces.repositories;

/// <summary>
/// Repository for managing DomainEvent entities with outbox pattern support.
/// </summary>
public interface IDomainEventRepository : IRepository<DomainEvent, Guid>
{
    Task<DomainEvent> CreateAsync(DomainEvent domainEvent);
    Task<IList<DomainEvent>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken ct);
    Task<IList<DomainEvent>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct);

    // Outbox methods
    Task<IList<DomainEvent>> GetPendingToPublishAsync(int batchSize, CancellationToken ct);
    Task MarkAsPublishedAsync(Guid id, CancellationToken ct);
    Task MarkAsFailedAsync(Guid id, string error, CancellationToken ct);
}
```

## EventStore Implementation

```csharp
using System.Reflection;
using System.Text.Json;
using {ProjectName}.domain.entities;
using {ProjectName}.domain.events;
using {ProjectName}.domain.interfaces;
using {ProjectName}.domain.interfaces.repositories;

namespace {ProjectName}.infrastructure.nhibernate;

public class EventStore(IUnitOfWork uoW) : IEventStore
{
    private readonly IUnitOfWork _uoW = uoW;

    public async Task AppendAsync<TEvent>(
        TEvent @event,
        Guid organizationId,
        string aggregateType,
        Guid aggregateId,
        Guid? userId = null,
        string? userName = null,
        string? ipAddress = null,
        string? correlationId = null) where TEvent : class
    {
        var shouldPublish = typeof(TEvent)
            .GetCustomAttribute<PublishableEventAttribute>() != null;

        var domainEvent = new DomainEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = typeof(TEvent).Name,
            EventData = JsonSerializer.Serialize(@event, GetJsonSerializerOptions()),
            OccurredAt = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            IpAddress = ipAddress,
            CorrelationId = correlationId ?? aggregateId.ToString(),
            ConversationId = Guid.NewGuid(),
            ShouldPublish = shouldPublish,
            PublishedAt = null,
            PublishAttempts = 0,
            Version = 1
        };

        await _uoW.DomainEvents.CreateAsync(domainEvent);
    }

    public async Task<IList<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken ct)
        => await _uoW.DomainEvents.GetByAggregateIdAsync(aggregateId, ct);

    public async Task<IList<DomainEvent>> GetEventsByOrganizationAsync(Guid organizationId, CancellationToken ct)
        => await _uoW.DomainEvents.GetByOrganizationIdAsync(organizationId, ct);

    private static JsonSerializerOptions GetJsonSerializerOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
```

## Database Migration

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

[Migration({MigrationNumber})]
public class M{MigrationNumber}CreateDomainEventsTable : Migration
{
    private readonly string _tableName = "domain_events";
    private readonly string _schemaName = "{SchemaName}";

    public override void Up()
    {
        Create.Table(_tableName)
            .InSchema(_schemaName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("organization_id").AsGuid().NotNullable()
            .WithColumn("aggregate_type").AsString(200).NotNullable()
            .WithColumn("aggregate_id").AsGuid().NotNullable()
            .WithColumn("event_type").AsString(200).NotNullable()
            .WithColumn("event_data").AsCustom("JSONB").NotNullable()
            .WithColumn("occurred_at").AsDateTime().NotNullable()
            .WithColumn("user_id").AsGuid().Nullable()
            .WithColumn("user_name").AsString(200).Nullable()
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("correlation_id").AsString(100).Nullable()
            .WithColumn("conversation_id").AsGuid().Nullable()
            .WithColumn("should_publish").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("published_at").AsDateTime().Nullable()
            .WithColumn("publish_attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("last_publish_error").AsCustom("TEXT").Nullable()
            .WithColumn("version").AsInt32().NotNullable().WithDefaultValue(1);

        Create.Index("ix_domain_events_organization")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("organization_id").Ascending()
            .OnColumn("occurred_at").Descending();

        Create.Index("ix_domain_events_aggregate")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("aggregate_id").Ascending()
            .OnColumn("occurred_at").Descending();

        Create.Index("ix_domain_events_correlation")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("correlation_id");

        Create.Index("ix_domain_events_outbox")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("should_publish").Ascending()
            .OnColumn("published_at").Ascending()
            .OnColumn("publish_attempts").Ascending()
            .OnColumn("occurred_at").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_domain_events_outbox").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_correlation").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_aggregate").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_organization").OnTable(_tableName).InSchema(_schemaName);
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

## Usage in Use Cases

```csharp
public class Handler(IUnitOfWork uoW, IEventStore eventStore, ILogger<Handler> logger)
    : ICommandHandler<Command, Result<ModuleUser>>
{
    public async Task<Result<ModuleUser>> ExecuteAsync(Command command, CancellationToken ct)
    {
        _uoW.BeginTransaction();
        try
        {
            // 1. Execute business logic
            var moduleUser = await _uoW.ModuleUsers.GetAsync(command.ModuleUserId, ct);
            var role = await _uoW.ModuleRoles.GetAsync(command.RoleId, ct);
            moduleUser.AddRole(role);

            // 2. Append domain event (same transaction)
            await _eventStore.AppendAsync(
                new RoleAddedToUserEvent(
                    OrganizationId: command.OrganizationId,
                    ModuleUserId: command.ModuleUserId,
                    ModuleUserName: moduleUser.Username,
                    RoleId: command.RoleId,
                    RoleName: role.Name),
                organizationId: command.OrganizationId,
                aggregateType: nameof(ModuleUser),
                aggregateId: command.ModuleUserId,
                userName: command.CurrentUserName);

            // 3. Commit atomically (state + event)
            _uoW.Commit();
            return Result.Ok(moduleUser);
        }
        catch
        {
            _uoW.Rollback();
            throw;
        }
    }
}
```

## Background Service (Outbox Processor)

```csharp
using {ProjectName}.domain.interfaces.repositories;

namespace {ProjectName}.infrastructure.services;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    public OutboxProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDomainEventRepository>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pendingEvents = await repository.GetPendingToPublishAsync(BatchSize, ct);

        foreach (var domainEvent in pendingEvents)
        {
            try
            {
                await messageBus.PublishAsync(
                    domainEvent.EventType,
                    domainEvent.EventData,
                    domainEvent.CorrelationId,
                    ct);

                await repository.MarkAsPublishedAsync(domainEvent.Id, ct);
            }
            catch (Exception ex)
            {
                await repository.MarkAsFailedAsync(domainEvent.Id, ex.Message, ct);

                _logger.LogWarning(ex,
                    "Failed to publish event {EventId}, attempt {Attempt}",
                    domainEvent.Id, domainEvent.PublishAttempts + 1);
            }
        }
    }
}
```

## Idempotency

Consumers must handle duplicate events (at-least-once delivery):

```csharp
// WRONG: Not idempotent
public async Task HandleOrderCreated(OrderCreatedEvent e)
{
    await _emailService.SendWelcomeEmail(e.CustomerId); // Sends multiple times
}

// CORRECT: Idempotent
public async Task HandleOrderCreated(OrderCreatedEvent e)
{
    if (await _emailLog.WasSent(e.OrderId, "welcome"))
        return; // Already processed

    await _emailService.SendWelcomeEmail(e.CustomerId);
    await _emailLog.MarkAsSent(e.OrderId, "welcome");
}
```

## Anti-Patterns

### Dual Write Problem

```csharp
// WRONG: Write to DB and bus without transaction
await _orderRepo.CreateAsync(order);
await _messageBus.PublishAsync(new OrderCreatedEvent(...)); // Can fail
```

Solution: Use the Outbox Pattern (append event inside the same transaction).

### Appending events outside the transaction

```csharp
// WRONG: Event outside transaction
await _uoW.Orders.CreateAsync(order);
_uoW.Commit();
await _eventStore.AppendAsync(...); // If this fails, event is lost
```

### Waiting for message bus confirmation

```csharp
// WRONG: Blocks the use case
await _eventStore.AppendAsync(...);
await _messageBus.PublishAndWaitAsync(...);

// CORRECT: Background service publishes async
await _eventStore.AppendAsync(...); // Only persists
```

## Implementation Checklist

**Domain Layer:**
- [ ] `DomainEvent` entity in `domain/entities/`
- [ ] `PublishableEventAttribute` in `domain/events/`
- [ ] `IEventStore` interface in `domain/interfaces/`
- [ ] `IDomainEventRepository` interface in `domain/interfaces/repositories/`
- [ ] `IDomainEventRepository` added to `IUnitOfWork`

**Infrastructure Layer:**
- [ ] `EventStore` implemented
- [ ] `{Prefix}DomainEventRepository` implemented
- [ ] `DomainEventMapper` for NHibernate created
- [ ] `IUnitOfWork` implementation updated with `DomainEvents` property
- [ ] `domain_events` table migration created with indexes
- [ ] Services registered in DI

**Application Layer:**
- [ ] Use cases call `IEventStore.AppendAsync()` inside transaction
- [ ] Events appended before `Commit()`
- [ ] Audit context included (userId, userName, ipAddress)
