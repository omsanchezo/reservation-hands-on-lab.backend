---
name: create-repository
description: >-
  Guides creation of NHibernate infrastructure components in APSYS .NET backend
  projects. Covers repository implementations (NHRepository, NHReadOnlyRepository),
  ClassMapping mappers, Unit of Work implementation, session factory configuration,
  and DI registration. Use when user asks to "create a mapper", "implement a repository",
  "configure NHibernate", "add a DAO mapper", "set up unit of work",
  or "write repository integration tests" in Clean Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with NHibernate and
  FluentValidation. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 2.0.0
---

# NHibernate Repository Pattern Skill

Guide for creating and maintaining NHibernate infrastructure components in APSYS .NET backend projects:
entity mappers, DAO mappers, repository implementations, Unit of Work, session factory, and DI configuration.

## Instructions

### Step 1: Identify the Component Type

Use the decision tree to determine what to create:

```
What do you need in the Infrastructure Layer?
|
+-- ENTITY MAPPER (map entity to table)
|   -> ClassMapping<T>, Schema + Table + Id + Properties
|   -> See references/mappers.md
|
+-- DAO MAPPER (map DAO to view, read-only)
|   -> ClassMapping<T>, Mutable(false)
|   -> See references/dao-mappers.md
|
+-- REPOSITORY (implement domain interface)
|   -> CRUD -> NHRepository<T, TKey>
|   -> Read-only -> NHReadOnlyRepository<T, TKey>
|   -> See references/repositories.md
|
+-- UNIT OF WORK (aggregate repositories)
|   -> NHUnitOfWork : IUnitOfWork, lazy property pattern
|   -> See references/unit-of-work.md
|
+-- SESSION FACTORY + DI
    -> NHSessionFactory, Singleton/Scoped/Scoped config
    -> See references/session-factory-and-di.md
```

### Step 2: Follow the Component Pattern

Each component has a specific pattern. Apply the pattern using the quick reference below for rules and conventions, and consult the corresponding file in `references/` for complete code and edge cases.

### Step 3: Validate with Checklist

Use the checklists at the end of this file to verify the generated code before finishing.

### Step 4: Create Tests

Every repository requires integration tests. See `references/integration-testing.md` for the full testing guide, including NHRepositoryTestBase, NDbUnit scenarios, and naming conventions. For comprehensive cross-layer testing guidance, use the `create-tests` skill.

---

## References

For complete implementations, additional examples, and edge cases, consult files in `references/`:

| Component | Reference |
|-----------|-----------|
| Entity Mappers | `references/mappers.md` |
| DAO Mappers | `references/dao-mappers.md` |
| Repositories | `references/repositories.md` |
| Unit of Work | `references/unit-of-work.md` |
| Session Factory and DI | `references/session-factory-and-di.md` |
| Integration Testing | `references/integration-testing.md` |

---

## Infrastructure Layer Structure

```
src/{project}.infrastructure/
├── nhibernate/
│   ├── NHSessionFactory.cs
│   ├── NHUnitOfWork.cs
│   ├── NHReadOnlyRepository.cs
│   ├── NHRepository.cs
│   ├── NHUserRepository.cs
│   ├── NHRoleRepository.cs
│   ├── NHPrototypeDaoRepository.cs
│   ├── filtering/
│   │   ├── FilterExpressionParser.cs
│   │   └── QueryStringParser.cs
│   └── mappers/
│       ├── UserMapper.cs
│       ├── RoleMapper.cs
│       ├── UserDaoMapper.cs
│       └── TechnicalStandardDaoMapper.cs
```

---

## 1. Entity Mappers

Every entity mapper inherits from `ClassMapping<T>` and maps a domain entity to a database table. Always configure in this order: Schema -> Table -> Id -> Properties -> Relations. For the full template and complete examples, see `references/mappers.md`.

### NHibernateUtil Type Mapping

| .NET Type | NHibernateUtil | PostgreSQL | SQL Server |
|-----------|----------------|------------|------------|
| `Guid` | `NHibernateUtil.Guid` | `UUID` | `UNIQUEIDENTIFIER` |
| `string` | `NHibernateUtil.String` | `VARCHAR` | `NVARCHAR` |
| `DateTime` | `NHibernateUtil.DateTime` | `TIMESTAMP` | `DATETIME2` |
| `int` | `NHibernateUtil.Int32` | `INTEGER` | `INT` |
| `long` | `NHibernateUtil.Int64` | `BIGINT` | `BIGINT` |
| `decimal` | `NHibernateUtil.Decimal` | `NUMERIC` | `DECIMAL` |
| `bool` | `NHibernateUtil.Boolean` | `BOOLEAN` | `BIT` |
| `byte[]` | `NHibernateUtil.BinaryBlob` | `BYTEA` | `VARBINARY` |

### Fundamental Rules

1. **Use `AppSchemaResource.SchemaName`** for schema (centralized constant)
2. **Always use `Generators.Assigned`** for Id (domain controls Id generation in DDD)
3. **Always specify `NHibernateUtil.*` type** explicitly on every property
4. **Set `NotNullable(true/false)`** to match database constraints
5. **No ORM attributes on entities** -- mapping is only in mapper classes

---

## 2. DAO Mappers

DAO mappers are for **read-only** Data Access Objects that map to database views. For the full template, view SQL patterns, and complete examples, see `references/dao-mappers.md`.

### Key Difference from Entity Mappers

| Aspect | Entity Mapper | DAO Mapper |
|--------|--------------|------------|
| Source | Table | View |
| `Mutable()` | `true` (default) | **`false`** |
| Operations | CRUD | Read-only |
| Repository | `NHRepository<T, TKey>` | `NHReadOnlyRepository<T, TKey>` |
| SearchAll | No | Yes (concatenated field) |

Key points: Always set `Mutable(false)` immediately after Schema. Always include a `SearchAll` property mapped to a concatenated view column using `CONCAT_WS(' ', LOWER(UNACCENT(...)))` in PostgreSQL.

---

## 3. Repositories

Repositories follow a hierarchy: `NHReadOnlyRepository` (read) -> `NHRepository` (CRUD + validation) -> Specific repositories. For full base class code, HQL with unaccent, and complete examples, see `references/repositories.md`.

### Repository Hierarchy

```
NHReadOnlyRepository<T, TKey>        <- Get, Count, GetManyAndCount
    |
    +-- NHRepository<T, TKey>        <- Add, Save, Delete + FluentValidation
         |
         +-- NHOrderRepository       <- CreateAsync, GetByNumberAsync, UpdateAsync
```

### Key Patterns

- **CreateAsync**: validate -> check duplicates -> persist -> `FlushWhenNotActiveTransaction()`
- **UpdateAsync**: get existing -> check duplicates (exclude self) -> update props -> validate -> persist -> flush
- **GetByXXXAsync**: null guard -> LINQ query with `ToLowerInvariant()` -> `SingleOrDefaultAsync()`
- **FlushWhenNotActiveTransaction**: flushes only if no active transaction (otherwise defers to `Commit()`)
- **Two-level validation**: Level 1 = domain (`IsValid()`), Level 2 = business (duplicates)
- **CRUD repos** receive `ISession` + `IServiceProvider` (for validators)
- **Read-only repos** receive only `ISession` (no validation needed)
- **Never** use `Add()` directly from use case -- always use `CreateAsync()`
- **Never** register repos in DI -- only register `IUnitOfWork`
- **Always** use `ToLowerInvariant()` on both sides of duplicate checks

---

## 4. Unit of Work

`NHUnitOfWork` implements `IUnitOfWork` (defined in domain layer) and aggregates all repositories using lazy property creation. For the full implementation, transaction methods, and Dispose pattern, see `references/unit-of-work.md`.

### Adding a New Repository

1. Add the interface property to `IUnitOfWork` (domain layer)
2. Add the lazy property to `NHUnitOfWork`:
   - **CRUD repo**: `=> new NHXRepository(_session, _serviceProvider);`
   - **Read-only repo**: `=> new NHReadOnlyRepository<XDao, Guid>(_session);`

Dispose order matters: transaction first, then session. The `_disposed` flag prevents double-dispose.

---

## 5. Session Factory and DI

For NHSessionFactory full code, PostgreSQL/SQL Server configs, ConfigureValidators, and Program.cs wiring, see `references/session-factory-and-di.md`.

### DI Lifetime Rules

| Component | Lifetime | Reason |
|-----------|----------|--------|
| `ISessionFactory` | **Singleton** | Thread-safe, expensive to create |
| `ISession` | **Scoped** | One session per HTTP request, NOT thread-safe |
| `IUnitOfWork` | **Scoped** | Wraps ISession, same lifetime |
| Repositories | **Transient** | Created on-demand by UnitOfWork |
| Validators | **Scoped** | Resolved by repositories via IServiceProvider |

Wire in Program.cs via `builder.Services.ConfigureUnitOfWork(configuration).ConfigureValidators()`. All `ClassMapping<T>` types in the assembly are auto-discovered via `mapper.AddMappings(typeof(RoleMapper).Assembly.ExportedTypes)` -- no manual registration needed.

---

## Checklists

### Entity Mapper
- [ ] Inherits `ClassMapping<Entity>`
- [ ] Namespace: `{project}.infrastructure.nhibernate.mappers`
- [ ] `Schema(AppSchemaResource.SchemaName)`
- [ ] `Table("snake_case_plural")`
- [ ] Id mapped with `Generators.Assigned` and `NHibernateUtil.Guid`
- [ ] All properties mapped with `NHibernateUtil.*` type
- [ ] `NotNullable()` set to match database constraints
- [ ] `Unique(true)` where applicable
- [ ] Relations mapped (Bag/ManyToMany/ManyToOne) if applicable
- [ ] XML doc summary added

### DAO Mapper
- [ ] Inherits `ClassMapping<EntityDao>`
- [ ] `Mutable(false)` set immediately after Schema
- [ ] Maps to a database view (`Table("entity_view")`)
- [ ] `SearchAll` property mapped
- [ ] No cascade or relationship mappings

### Repository
- [ ] CRUD: inherits `NHRepository<Entity, Guid>`, implements `IEntityRepository`
- [ ] Read-only: inherits `NHReadOnlyRepository<EntityDao, Guid>`
- [ ] CRUD constructor receives `ISession` and `IServiceProvider`
- [ ] `CreateAsync()` method: validate -> check duplicates -> persist -> flush
- [ ] `UpdateAsync()` method: get existing -> check duplicates (exclude self) -> update -> validate -> flush
- [ ] `GetByXXXAsync()` methods: null guard, `ToLowerInvariant()`, `SingleOrDefaultAsync()`
- [ ] `FlushWhenNotActiveTransaction()` called after every persist operation
- [ ] Domain exceptions thrown: `InvalidDomainException`, `DuplicatedDomainException`, `ResourceNotFoundException`

### UnitOfWork Entry
- [ ] Interface property added to `IUnitOfWork` (domain layer)
- [ ] Lazy property added to `NHUnitOfWork`
- [ ] CRUD repos use `_session` + `_serviceProvider`
- [ ] Read-only repos use only `_session`

---

## Examples

### Example 1: Create mapper + repository for Order entity

User says: "Create a mapper and repository for Order"

Actions:
1. Create `OrderMapper.cs` in `nhibernate/mappers/` with `ClassMapping<Order>`
2. Configure: Schema, Table("orders"), Id, all properties with NHibernateUtil types
3. Create `NHOrderRepository.cs` inheriting `NHRepository<Order, Guid>`, implementing `IOrderRepository`
4. Implement `CreateAsync()` with domain validation, duplicate check, `AddAsync`, and `FlushWhenNotActiveTransaction()`
5. Implement `GetByXXXAsync()` methods with `ToLowerInvariant()` and `SingleOrDefaultAsync()`
6. Add `IOrderRepository Orders` property to `IUnitOfWork` interface
7. Add lazy property `public IOrderRepository Orders => new NHOrderRepository(_session, _serviceProvider);` to `NHUnitOfWork`
8. Register `AbstractValidator<Order>` in `ConfigureValidators()`

Result: `OrderMapper.cs` and `NHOrderRepository.cs` created. UnitOfWork updated. Ready for use case integration.

### Example 2: Add DAO mapper for OrderDao with read-only repo

User says: "I need a DAO mapper for listing orders"

Actions:
1. Create `OrderDaoMapper.cs` with `ClassMapping<OrderDao>`, `Mutable(false)`, `Table("orders_view")`
2. Map all properties including `SearchAll`
3. Create the PostgreSQL view `orders_view` with `CONCAT_WS` + `LOWER` + `UNACCENT` for SearchAll
4. Create `NHOrderDaoRepository.cs` inheriting `NHReadOnlyRepository<OrderDao, Guid>`
5. Add `IReadOnlyRepository<OrderDao, Guid> OrderDaos` to `IUnitOfWork`
6. Add lazy property `=> new NHReadOnlyRepository<OrderDao, Guid>(_session)` to `NHUnitOfWork`

Result: `OrderDaoMapper.cs` and read-only repo created. No IServiceProvider needed for read-only repos.

### Example 3: Configure NHibernate from scratch

User says: "Configure NHibernate for a new project"

Actions:
1. Create `NHSessionFactory.cs` with `BuildNHibernateSessionFactory()` method
2. Configure: `ModelMapper`, auto-registration via `AddMappings`, `NpgsqlDriver`, `PostgreSQL83Dialect`
3. Create `ConnectionStringBuilder.cs` reading from environment variables
4. Create `ServiceCollectionExtender.cs` with `ConfigureUnitOfWork()` and `ConfigureValidators()` methods
5. Wire in `Program.cs`: `builder.Services.ConfigureUnitOfWork(configuration).ConfigureValidators()`
6. Create `NHUnitOfWork.cs` with lazy repo properties and Dispose pattern

Result: Full NHibernate infrastructure bootstrapped. SessionFactory(Singleton), ISession(Scoped), IUnitOfWork(Scoped).

---

## Troubleshooting

### No persister for entity (mapper not found)
Cause: The mapper class is not in the same assembly as the reference mapper used in `AddMappings()`, or the class does not inherit `ClassMapping<T>`.
Solution: Ensure the mapper is `public`, inherits `ClassMapping<T>`, and is in the assembly scanned by `mapper.AddMappings(typeof(RoleMapper).Assembly.ExportedTypes)`.

### Could not determine type for column
Cause: Missing `map.Type(NHibernateUtil.*)` on a property mapping.
Solution: Always specify the NHibernateUtil type explicitly for every property. Check the type mapping table in Section 1.

### Invalid object name / relation does not exist
Cause: Wrong schema or table name in the mapper.
Solution: Verify `Schema(AppSchemaResource.SchemaName)` matches the database schema, and `Table("name")` matches the actual table or view name (case-sensitive in PostgreSQL).

### Could not find validator for entity
Cause: `AbstractValidator<T>` is not registered in DI, or the generic type resolution fails in `NHRepository` constructor.
Solution: Register the validator in `ConfigureValidators()`: `services.AddScoped<AbstractValidator<Order>, OrderValidator>();`. Ensure the entity's validator class exists.

### Changes not persisted (no error thrown)
Cause: Missing `FlushWhenNotActiveTransaction()` after `AddAsync()` or `SaveAsync()` in the repository, and no active transaction to trigger flush on `Commit()`.
Solution: Always call `this.FlushWhenNotActiveTransaction()` after every persist operation in specific repository methods (`CreateAsync`, `UpdateAsync`).

### StaleObjectStateException on save
Cause: Using `Add()` / `AddAsync()` (INSERT) for an entity that already exists, or `Save()` / `SaveAsync()` (UPDATE) for a new entity.
Solution: Use `AddAsync()` only for new entities. Use `_session.UpdateAsync()` for existing entities. In `CreateAsync`, always use `AddAsync`. In `UpdateAsync`, use `_session.UpdateAsync`.

### Duplicate created despite check
Cause: Duplicate check uses case-sensitive comparison, missing `ToLowerInvariant()`.
Solution: Always use `ToLowerInvariant()` on both sides of the comparison: `p => p.Code.ToLowerInvariant() == code.ToLowerInvariant()`. For accent-insensitive searches, use HQL with `unaccent()`.

---

## Related

- **Domain Layer:** `create-domain` -- Entities, validators, DAOs, repository interfaces (IRepository, IUnitOfWork)
- **Application Layer:** `create-use-case` -- Use cases, Command/Handler pattern, transaction management
- **WebApi Layer:** `create-endpoint` -- Endpoints, request/response models, DTOs, AutoMapper profiles
- **Testing:** See `references/integration-testing.md` -- Unit tests, integration tests, conventions
