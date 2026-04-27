## Context

<!-- Background, current state, constraints.
     What exists today that this change builds on or modifies? -->

## Goals / Non-Goals

**Goals:**
<!-- What this design achieves -->

**Non-Goals:**
<!-- Explicitly out of scope -->

## Domain Model

<!-- Entity names, relationships, value objects.
     Simple text diagram:
     User 1──N Role
     User has ValueObject: Email, PhoneNumber -->

## Operations Scope

<!-- For each entity, which operations?
| Entity | Create | Get | GetManyAndCount | Update | Delete |
|--------|--------|-----|-----------------|--------|--------|
| User   | ✓      | ✓   | ✓               | ✓      | ✗      | -->

## Database Design

<!-- What tables/columns are needed? New tables or alterations?
     Document the choice AND rationale:
     - Schema name and why
     - Column types (.NET → DB mapping)
     - Migration strategy: new tables vs alterations
     - If adding required columns: what backfill strategy? -->

## NHibernate Strategy

<!-- For each entity, what mapping decisions and why?
     - Relationship types (ManyToOne, Bag, OneToOne) — why this type?
     - Cascade behavior per relationship — why All vs SaveUpdate vs None?
     - Lazy loading exceptions — which relationships are eagerly loaded and why? -->

## Read Model Design

<!-- ONLY for entities with GetManyAndCount.

     For each DAO:
     ### <Entity>Dao
     - **Source view**: <schema>.<entity_view>
     - **DAO fields**: What fields does the list view need? (flattened, no navigation)
     - **SearchAll composition**: Which fields are searchable and why?
     - **View SQL strategy**: Which joins are needed to denormalize?
     - **Filters**: What filtering does the user need? -->

## Endpoint Design

<!-- What routes and contracts?
     - RESTful conventions: what routes for this feature?
     - Request/response shapes: what fields, which are required?
     - Error-to-HTTP mapping:
       InvalidDomainException → 400, DuplicatedDomainException → 409,
       ResourceNotFoundException → 404
     - Any deviations from the standard pattern and why? -->

## Cross-Cutting Decisions

<!-- Include ONLY sections that apply to this feature. Delete the rest.

### Domain Events
Are events needed? Synchronous or via outbox? What events and who consumes them?

### Date/Time Handling
Are there timezone considerations? What fields store dates and in what format?

### Transaction Isolation
Are there concurrency risks? Does any operation need a specific isolation level?

### Error Handling Strategy
Does this feature follow the standard error flow or need a different approach? Why?
-->

## Risks / Trade-offs

<!-- Known limitations. Format: [Risk] → Mitigation -->

## Migration Plan

<!-- Deploy steps, rollback strategy. Only if applicable. -->

## Open Questions

<!-- Unresolved decisions or unknowns to clarify before coding. -->
