---
name: create-domain
description: Guides creation of Domain Layer components in APSYS .NET backend projects. Covers entities (AbstractDomainObject), FluentValidation validators, value objects, domain exceptions, DAOs, and repository interfaces. Use when user asks to "create an entity", "add validations", "define a repository interface", "create a DAO", "add a value object", or "work with domain exceptions" in Clean Architecture.
compatibility: Requires .NET backend projects using Clean Architecture with NHibernate and FluentValidation. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 2.0.0
---

# Domain Layer Skill

Guide for creating and maintaining Domain Layer components in APSYS .NET backend projects:
entities, validators, value objects, domain exceptions, DAOs, and repository interfaces.

## Instructions

### Step 1: Identify the Component Type

Use the decision tree to determine what to create:

```
What do you need in the Domain Layer?
|
+-- ENTITY (object with identity, business rules)
|   -> Inherit AbstractDomainObject
|   -> Create Validator in validators/
|
+-- VALUE OBJECT (immutable type defined by value)
|   -> Use sealed record with init
|
+-- DAO (read-only queries)
|   -> POCO with SearchAll, no AbstractDomainObject
|
+-- DOMAIN EXCEPTION
|   -> Validation failed -> InvalidDomainException
|   -> Duplicate -> DuplicatedDomainException
|   -> Not found -> ResourceNotFoundException
|   -> Invalid filter -> InvalidFilterArgumentException
|
+-- REPOSITORY INTERFACE
    -> CRUD -> IRepository<T, Guid>
    -> Read-only -> IReadOnlyRepository<T, Guid>
    -> Register in IUnitOfWork
```

### Step 2: Follow the Component Pattern

Each component has a specific pattern documented below (sections 1-6). Apply the pattern, consulting `references/` for detailed examples and edge cases.

### Step 3: Validate with Checklist

Use the checklists at the end of this file to verify the generated code before finishing.

### Step 4: Create Tests

Every entity requires unit tests. See `references/domain-testing.md` for the full testing guide, including base class, AutoFixture patterns, and naming conventions. For comprehensive cross-layer testing guidance, use the `create-tests` skill.

---

## References

For complete patterns, additional examples, and edge cases, consult files in `references/`:

| Component | Reference |
|-----------|-----------|
| Entities | `references/entities.md` |
| Validators | `references/validators.md` |
| Value Objects | `references/value-objects.md` |
| Exceptions | `references/domain-exceptions.md` |
| DAOs | `references/daos.md` |
| Repository Interfaces | `references/repository-interfaces.md` |
| Domain Testing | `references/domain-testing.md` |

---

## Domain Layer Structure

```
src/{project}.domain/
├── entities/
│   ├── User.cs
│   ├── Role.cs
│   └── validators/
│       ├── UserValidator.cs
│       └── RoleValidator.cs
├── daos/
│   └── UserDao.cs
├── exceptions/
│   ├── InvalidDomainException.cs
│   ├── DuplicatedDomainException.cs
│   ├── ResourceNotFoundException.cs
│   └── InvalidFilterArgumentException.cs
├── resources/
│   └── UserResource.cs
└── interfaces/
    ├── IUserRepository.cs
    └── IUnitOfWork.cs
```

---

## 1. Entities

Every entity inherits from `AbstractDomainObject`, which provides: `Id` (Guid), `CreationDate` (DateTime UTC), `IsValid()`, `Validate()`, and `GetValidator()`.

### Fundamental Rules

1. **Inherit from `AbstractDomainObject`**
2. **All properties are `virtual`** (required by NHibernate)
3. **Exactly two constructors**: empty (NHibernate) + parameterized (creation)
4. **Override `GetValidator()`** to integrate FluentValidation
5. **Initialize properties** with defaults (`string.Empty`, `new List<>()`)
6. **No ORM attributes** or persistence logic

### Complete Pattern

```csharp
namespace {project}.domain.entities;

public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual bool Locked { get; set; }
    public virtual IList<Role> Roles { get; set; } = new List<Role>();

    /// <summary>
    /// Constructor used by NHibernate for mapping purposes.
    /// </summary>
    public User() { }

    /// <summary>
    /// Initializes a new instance with the specified values.
    /// </summary>
    public User(string email, string name)
    {
        Email = email;
        Name = name;
    }

    public override IValidator GetValidator() => new UserValidator();
}
```

### Parameterized Constructor: What to Include

- **YES:** properties essential for creation
- **NO:** `Id`, `CreationDate` (auto-assigned), collections (initialized at property)

### Anti-Patterns

```csharp
// NEVER do this:
[Table("users")]                    // ORM attribute
public class User : AbstractDomainObject
{
    public string Name { get; set; }    // Missing virtual
    public void SaveToDatabase() { }    // Persistence logic
}
```

> For more examples (simple, medium, complex), see `references/entities.md`

---

## 2. Validators

One validator per entity. Inherit from `AbstractValidator<T>` (FluentValidation).

### Base Pattern

```csharp
using FluentValidation;

namespace {project}.domain.entities.validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Email)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Email] cannot be null or empty")
            .WithErrorCode("Email")
            .EmailAddress()
            .WithMessage("The [Email] is not a valid email address")
            .WithErrorCode("Email_InvalidDomain");

        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Name] cannot be null or empty")
            .WithErrorCode("Name");
    }
}
```

### Common Validations

| Type | Validation | Example |
|------|-----------|---------|
| String | `NotEmpty()` | Required, includes NotNull |
| String | `MaximumLength(n)` | Character limit |
| String | `EmailAddress()` | Email format |
| Number | `GreaterThan(n)` | Exclusive minimum |
| Number | `InclusiveBetween(a,b)` | Range |
| Date | `LessThan(DateTime.Today)` | Not in future |
| Cross-property | `GreaterThan(x => x.OtherProp)` | Compare properties |
| Custom | `Must((entity, val) => ...)` | Custom predicate |

### WithMessage + WithErrorCode

**Always** use both. Chain per validation group:

```csharp
RuleFor(x => x.Status)
    .NotEmpty()
    .WithMessage("The [Status] cannot be null or empty")
    .WithErrorCode("Status")
    .Must(s => AllowedStatuses.Contains(s))
    .WithMessage("The [Status] must be one of: Active, Inactive")
    .WithErrorCode("Status_InvalidValue");
```

### Resources for Constants (Avoid Magic Strings)

```csharp
public static class UserResource
{
    public const string StatusActive = "Active";
    public const string StatusInactive = "Inactive";
    public static readonly string[] ValidStatuses = { StatusActive, StatusInactive };
}
```

> For custom, conditional, and cross-property validations, see `references/validators.md`

---

## 3. Value Objects

Immutable, equality by value, no identity. Use `sealed record` with `init` properties.

### Pattern

```csharp
public sealed record Email
{
    public string Value { get; init; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format");
        Value = value;
    }
}
```

### Characteristics

- **Immutable:** `init` properties, not `set`
- **Self-validating:** constructor validates and throws exceptions
- **Equality by value:** `record` gives automatic structural equality
- **No identity:** does not inherit from `AbstractDomainObject`
- **`sealed`:** prevent accidental inheritance

### When to Use

- **YES:** Email, Money, Address, DateRange, PhoneNumber -- types defined by their value
- **NO:** if it only has a simple property without rules, use the primitive type directly

> For more examples (Money, DateRange) and NHibernate integration, see `references/value-objects.md`

---

## 4. Domain Exceptions

### The 4 Types

| Exception | When to Use | HTTP Status |
|-----------|-------------|-------------|
| `InvalidDomainException` | Entity fails validation | 400 |
| `DuplicatedDomainException` | Duplicate record | 409 |
| `ResourceNotFoundException` | Resource not found | 404 |
| `InvalidFilterArgumentException` | Invalid query filter | 400 |

### Usage

```csharp
// InvalidDomainException - from entity validation
if (!entity.IsValid())
    throw new InvalidDomainException(entity.Validate());

// InvalidDomainException - manual with property, code, and message
throw new InvalidDomainException("Email", "Email_Duplicate", "Email already exists");

// DuplicatedDomainException
var existing = await repository.GetByEmail(email);
if (existing != null)
    throw new DuplicatedDomainException($"User with email {email} already exists");

// ResourceNotFoundException
var entity = await repository.GetAsync(id);
if (entity == null)
    throw new ResourceNotFoundException($"User with id {id} not found");

// InvalidFilterArgumentException
if (string.IsNullOrEmpty(filter.Status))
    throw new InvalidFilterArgumentException("Status filter is required");
```

> For constructor details and Serialize(), see `references/domain-exceptions.md`

---

## 5. DAOs (Data Access Objects)

**Read-only** objects for optimized queries. Do not inherit from `AbstractDomainObject`.

### Pattern

```csharp
namespace {project}.domain.daos;

public class UserDao
{
    public virtual Guid Id { get; set; }
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual string SearchAll { get; set; } = string.Empty;
}
```

### Rules

1. **No `AbstractDomainObject` inheritance** -- simple POCOs
2. **`virtual` properties** -- required by NHibernate
3. **No navigation properties** -- flatten relationships
4. **`SearchAll` field** -- concatenation of searchable fields for full-text search
5. **No validation** -- read-only
6. **Repository `IReadOnlyRepository<T, Guid>`** -- no write operations

> For Entity vs DAO vs DTO comparison and more examples, see `references/daos.md`

---

## 6. Repository Interfaces

Interfaces in Domain, implementations in Infrastructure (Dependency Inversion).

### Hierarchy

```
IReadOnlyRepository<T, TKey>    <- Get, Count, GetManyAndCount (read)
    +-- IRepository<T, TKey>    <- Add, Save, Delete (write)
```

### Key Methods

| Method | Description |
|--------|-------------|
| `Get(Expression<Func<T, bool>>)` | Get one by condition |
| `GetAsync(TKey)` | Get by Id |
| `GetManyAndCountAsync(Expression, int page, int size, ...)` | Pagination |
| `Add(T)` / `AddAsync(T)` | Insert new entity |
| `Save(T)` / `SaveAsync(T)` | Update existing entity |
| `Delete(T)` / `DeleteAsync(T)` | Delete entity |

**Add vs Save:** `Add` for new entities, `Save` for existing ones. Using the wrong one causes errors.

### Specific Interfaces

```csharp
public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmail(string email);
}
```

### IUnitOfWork

Aggregates all repositories and transaction management:

```csharp
public interface IUnitOfWork
{
    // Repositories
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }

    // Read-only (DAOs)
    IReadOnlyRepository<UserDao, Guid> UserDaos { get; }

    // Transactions
    void Commit();
    void Rollback();
    void BeginTransaction();
}
```

> For support types (SortingCriteria, etc.) and usage examples, see `references/repository-interfaces.md`

---

## Checklists

### New Entity
- [ ] Inherits `AbstractDomainObject`
- [ ] Namespace: `{project}.domain.entities`
- [ ] `virtual` properties with default values
- [ ] Empty constructor + parameterized constructor
- [ ] `GetValidator()` overridden
- [ ] Validator created in `validators/`
- [ ] No ORM attributes or persistence logic
- [ ] Unit tests created

### New Validator
- [ ] Inherits `AbstractValidator<Entity>`
- [ ] Namespace: `{project}.domain.entities.validators`
- [ ] `RuleFor()` for each property requiring validation
- [ ] `WithMessage()` + `WithErrorCode()` on every rule
- [ ] Resources for constants (if allowed values exist)
- [ ] Integrated with entity via `GetValidator()`

### New DAO
- [ ] POCO without `AbstractDomainObject` inheritance
- [ ] Namespace: `{project}.domain.daos`
- [ ] `virtual` properties
- [ ] `SearchAll` field (string)
- [ ] No navigation properties (flattened relationships)
- [ ] Registered in `IUnitOfWork` as `IReadOnlyRepository`

### New Repository Interface
- [ ] Inherits `IRepository<Entity, Guid>` or `IReadOnlyRepository<Entity, Guid>`
- [ ] Namespace: `{project}.domain.interfaces`
- [ ] Custom domain methods defined (if applicable)
- [ ] Registered in `IUnitOfWork`

---

## Examples

### Example 1: Create a new entity with validator

User says: "Create an Order entity with OrderNumber, Total, and Status"

Actions:
1. Create `Order.cs` in `entities/` inheriting `AbstractDomainObject`
2. Add `virtual` properties with defaults: `OrderNumber` (string), `Total` (decimal), `Status` (string)
3. Add empty constructor + parameterized constructor with `orderNumber`, `total`, `status`
4. Override `GetValidator()` returning `new OrderValidator()`
5. Create `OrderValidator.cs` in `entities/validators/` with rules for each property
6. Create `OrderResource.cs` in `resources/` for status constants
7. Create unit tests for constructors, validation, and GetValidator

Result: Two files created (`Order.cs`, `OrderValidator.cs`) plus optional `OrderResource.cs` and tests. Entity is ready for NHibernate mapping in Infrastructure layer.

### Example 2: Add a DAO for listing

User says: "I need a DAO to list orders with search"

Actions:
1. Create `OrderDao.cs` in `daos/` as a simple POCO
2. Add `virtual` properties: `Id`, `OrderNumber`, `CustomerName`, `Total`, `Status`, `SearchAll`
3. Flatten relationships (e.g., `CustomerName` instead of `Customer` navigation property)
4. Register `IReadOnlyRepository<OrderDao, Guid>` in `IUnitOfWork`

Result: `OrderDao.cs` created. Ready for NHibernate mapping with Formula for `SearchAll`.

### Example 3: Define a repository interface

User says: "Create a repository interface for Order with a method to find by status"

Actions:
1. Create `IOrderRepository.cs` in `interfaces/` inheriting `IRepository<Order, Guid>`
2. Add custom method: `Task<IList<Order>> GetByStatusAsync(string status)`
3. Register `IOrderRepository` in `IUnitOfWork`

Result: `IOrderRepository.cs` created. Implementation will be in Infrastructure layer.

---

## Troubleshooting

### Error: NHibernate proxy generation failed
Cause: One or more properties are missing the `virtual` keyword.
Solution: Ensure ALL properties in entities and DAOs are declared as `virtual`. This includes navigation properties and collections.

### Error: GetValidator() not found or not overridden
Cause: Entity does not override `GetValidator()` from `AbstractDomainObject`.
Solution: Add `public override IValidator GetValidator() => new {Entity}Validator();` to the entity class. Ensure the validator class exists in `entities/validators/`.

### Error: StaleObjectStateException on save
Cause: Using `Add()` for an existing entity or `Save()` for a new entity.
Solution: Use `Add()` / `AddAsync()` only for new entities (INSERT). Use `Save()` / `SaveAsync()` only for existing entities (UPDATE).

### Error: InvalidDomainException without useful error details
Cause: Validator rules missing `WithMessage()` or `WithErrorCode()`.
Solution: Every validation rule must chain both `.WithMessage("The [{Property}] ...")` and `.WithErrorCode("{Property}")`. See section 2 for the pattern.

### Error: Collection not initialized (NullReferenceException)
Cause: `IList<T>` property not initialized at declaration.
Solution: Always initialize collections at the property level: `public virtual IList<Role> Roles { get; set; } = new List<Role>();`. Never initialize collections in the constructor.

### Error: Id or CreationDate is empty/default
Cause: Manually setting `Id` or `CreationDate` instead of letting `AbstractDomainObject` handle them.
Solution: Never include `Id` or `CreationDate` in the parameterized constructor. They are auto-generated by the base class.

---

## Related

- **Application Layer:** `create-use-case` -- Use cases, Command/Handler pattern, error handling
- **Testing:** See `references/domain-testing.md` -- Domain tests, integration tests, conventions
- **Other architecture layer skills:**
  - `create-repository` -- NHibernate mappers, repository implementations
  - `create-endpoint` -- Endpoints, middleware, request/response
