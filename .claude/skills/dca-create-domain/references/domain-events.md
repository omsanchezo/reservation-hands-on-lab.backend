# Domain Events

Domain Events are immutable objects representing **something that happened** in the business domain. They capture important facts that other system components may need to know about.

## Event Types

### Audit-Only Events

Persisted for tracking and compliance only. Not published to external systems.

```csharp
// No attribute = audit only
public record RoleAddedToUserEvent(
    Guid OrganizationId,
    Guid ModuleUserId,
    string ModuleUserName,
    Guid RoleId,
    string RoleName);
```

Use cases: change history, compliance, debugging, state reconstruction.

### Publishable Events

Persisted AND published to the message bus to notify other services.

```csharp
[PublishableEvent]
public record UserAccessGrantedEvent(
    Guid OrganizationId,
    Guid ModuleUserId,
    string ModuleUserName,
    Guid UserId);
```

Use cases: cross-service notifications, external workflow triggers, data sync, push/email notifications.

**Decision rule:** Does the event need to notify other services? Yes -> `[PublishableEvent]`. No -> no attribute.

## Modeling Events

### Use Records

```csharp
// Record = immutable by default, value equality, concise syntax
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt);
```

### Full Event Structure

```csharp
namespace {ProjectName}.domain.events.orders;

/// <summary>
/// Raised when a new order is created.
/// </summary>
/// <remarks>
/// This event is published to notify inventory and shipping services.
/// </remarks>
[PublishableEvent]
public record OrderCreatedEvent(
    // Aggregate identification
    /// <summary>Organization for multi-tenant filtering.</summary>
    Guid OrganizationId,

    /// <summary>The unique identifier of the created order.</summary>
    Guid OrderId,

    // Event data
    /// <summary>Customer who placed the order.</summary>
    Guid CustomerId,

    /// <summary>Total amount of the order.</summary>
    decimal TotalAmount,

    /// <summary>Number of items in the order.</summary>
    int ItemCount,

    // Metadata
    /// <summary>When the order was created.</summary>
    DateTime CreatedAt
);
```

### What to Include vs Exclude

| Include | Do NOT Include |
|---------|----------------|
| Identifiers (Guid) | Full entities |
| Data needed by consumers | Sensitive data (passwords, cards) |
| Relevant timestamps | Navigable relationships |
| Simple calculated values | Large collections |
| Short descriptive strings | Blobs or files |

## File Organization

```
src/{project}.domain/
└── events/
    ├── PublishableEventAttribute.cs    <- Shared attribute
    │
    ├── orders/                         <- Per feature/aggregate
    │   ├── OrderCreatedEvent.cs
    │   ├── OrderCancelledEvent.cs
    │   ├── OrderShippedEvent.cs
    │   └── PaymentReceivedEvent.cs
    │
    ├── users/
    │   ├── UserCreatedEvent.cs
    │   ├── UserDeactivatedEvent.cs
    │   ├── RoleAddedToUserEvent.cs
    │   └── UserAccessGrantedEvent.cs
    │
    └── inventory/
        ├── StockUpdatedEvent.cs
        └── LowStockAlertEvent.cs
```

**One event per file:**

```csharp
// File: events/orders/OrderCreatedEvent.cs
namespace {ProjectName}.domain.events.orders;

[PublishableEvent]
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId);
```

```csharp
// WRONG: Multiple events in one file
public record OrderCreatedEvent(...);
public record OrderCancelledEvent(...);
public record OrderShippedEvent(...);
```

## Naming Conventions

| Pattern | Example |
|---------|---------|
| `{Noun}{Verb}Event` | `OrderCreatedEvent` |
| `{Noun}{Action}Event` | `PaymentProcessedEvent` |
| `{Noun}{State}Event` | `OrderShippedEvent` |

Common verbs: `Created`, `Updated`, `Deleted`, `Activated`, `Deactivated`, `Shipped`, `Sent`, `Processed`, `Completed`, `Cancelled`, `Approved`, `Rejected`.

## Best Practices

### Small, specific events

```csharp
// CORRECT: Specific event
public record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    DateTime ShippedAt);

// WRONG: Generic event
public record OrderUpdatedEvent(
    Guid OrderId,
    string FieldName,
    object OldValue,
    object NewValue);
```

### Always include OrganizationId for multi-tenancy

```csharp
public record OrderCreatedEvent(
    Guid OrganizationId,
    Guid OrderId,
    Guid CustomerId);
```

### Document with XML comments

```csharp
/// <summary>
/// Raised when an organization activates a new module.
/// </summary>
/// <remarks>
/// This event triggers:
/// - User provisioning in the module
/// - Billing system notification
/// - Welcome email to admin
/// </remarks>
[PublishableEvent]
public record OrganizationModuleActivatedEvent(
    /// <summary>The organization that activated the module.</summary>
    Guid OrganizationId,
    /// <summary>User who performed the activation.</summary>
    Guid ActivatedByUserId,
    /// <summary>The plan selected for the module.</summary>
    Guid PlanId);
```

### Use specific types

```csharp
// CORRECT
public record PaymentProcessedEvent(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    PaymentMethod Method);   // Enum

// WRONG
public record PaymentProcessedEvent(
    string PaymentId,        // String instead of Guid
    double Amount,           // Double instead of decimal
    string Method);          // String instead of enum
```

## Anti-Patterns

### Do NOT include full entities

```csharp
// WRONG
public record OrderCreatedEvent(Order FullOrder);

// CORRECT
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount);
```

### Do NOT use present/future tense names

```csharp
// WRONG
public record CreateOrderEvent(...);       // Imperative
public record OrderWillShipEvent(...);     // Future
public record ProcessingPaymentEvent(...); // Present continuous

// CORRECT
public record OrderCreatedEvent(...);
public record OrderShippedEvent(...);
public record PaymentProcessedEvent(...);
```

### Do NOT add logic to events

```csharp
// WRONG
public record OrderCreatedEvent(Guid OrderId)
{
    public bool IsHighValue => TotalAmount > 1000; // Logic
    public void SendNotification() { ... }         // Behavior
}

// CORRECT: Data only
public record OrderCreatedEvent(
    Guid OrderId,
    decimal TotalAmount);
```

## Creation Checklist

- [ ] Name in past tense (`{Noun}{Verb}Event`)
- [ ] Use `record` (not `class`)
- [ ] Include `OrganizationId` for multi-tenancy
- [ ] Include aggregate identifier (`{Aggregate}Id`)
- [ ] Only necessary data (no full entities)
- [ ] No sensitive data (passwords, cards)
- [ ] XML documentation with `<summary>`
- [ ] Separate file in `domain/events/{feature}/`
- [ ] Decide if it needs `[PublishableEvent]`
