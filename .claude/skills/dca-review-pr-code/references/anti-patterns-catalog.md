# Anti-Patterns Catalog

Catalog of anti-patterns with BAD/GOOD examples for reviewing APSYS .NET backend code.

---

## 1. Architecture Anti-Patterns (CRITICO)

### 1.1 Business Logic in Handlers

**Severity:** CRITICO
**Impact:** Violates Clean Architecture — business logic belongs in domain entities and validators, not in application handlers. Handlers should only orchestrate: begin transaction, call domain/repository, commit/rollback.

```csharp
// BAD: Business logic in handler
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    unitOfWork.BeginTransaction();

    // VIOLATION: validation logic belongs in entity/validator
    if (string.IsNullOrEmpty(command.Email))
        return Result.Fail<User>("Email is required");

    if (!command.Email.Contains('@'))
        return Result.Fail<User>("Invalid email");

    // VIOLATION: business rule belongs in domain
    if (command.Name.Length > 100)
        return Result.Fail<User>("Name too long");

    var user = new User(command.Email, command.Name);
    await unitOfWork.Users.CreateAsync(user);
    unitOfWork.Commit();
    return Result.Ok(user);
}

// GOOD: Handler delegates to domain
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    try
    {
        unitOfWork.BeginTransaction();

        var user = new User(command.Email, command.Name);
        await unitOfWork.Users.CreateAsync(user);  // Validate() called inside repository

        unitOfWork.Commit();
        return Result.Ok(user);
    }
    catch (InvalidDomainException ex)
    {
        return Result.Fail<User>(/* validation errors from entity validator */);
    }
    // ...
}
```

| Check | Severity |
|-------|----------|
| Validation logic in handler instead of entity/validator | Critical |
| Business rules in handler instead of domain | Critical |
| String validation or null checks in handler that duplicate validator rules | Critical |

### 1.2 Persistence Logic in Domain Layer

**Severity:** CRITICO
**Impact:** Domain layer must have zero dependencies on Infrastructure. ORM attributes, ISession references, or repository calls in entities violate the innermost layer rule.

```csharp
// BAD: ORM attributes on entity
using NHibernate;  // VIOLATION: infrastructure dependency in domain

[Table("users")]  // VIOLATION: ORM attribute
public class User : AbstractDomainObject
{
    [Column("email")]  // VIOLATION
    public virtual string Email { get; set; } = string.Empty;
}

// BAD: Persistence method in entity
public class User : AbstractDomainObject
{
    public async Task SaveAsync(ISession session)  // VIOLATION: persistence in domain
    {
        await session.SaveAsync(this);
    }
}

// GOOD: Clean entity — no ORM or infrastructure references
namespace project.domain.entities;

public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;

    public User() { }
    public User(string email, string name) { Email = email; Name = name; }
    public override IValidator GetValidator() => new UserValidator();
}
```

| Check | Severity |
|-------|----------|
| Entity has ORM attributes ([Table], [Column], etc.) | Critical |
| Entity references NHibernate or ISession | Critical |
| Entity has persistence methods (Save, Delete, etc.) | Critical |
| Domain project imports Infrastructure namespace | Critical |

### 1.3 Direct ISession Usage in Handlers

**Severity:** CRITICO
**Impact:** Handlers must access data through repositories via IUnitOfWork. Direct ISession usage bypasses the repository pattern and couples Application to Infrastructure.

```csharp
// BAD: Handler using ISession directly
public class Handler(
    ISession session,  // VIOLATION: direct ISession dependency
    ILogger<Handler> logger) : ICommandHandler<Command, Result<User>>
{
    public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
    {
        var user = await session.GetAsync<User>(command.Id);  // VIOLATION
        await session.SaveAsync(user);  // VIOLATION
        return Result.Ok(user);
    }
}

// GOOD: Handler uses IUnitOfWork
public class Handler(
    IUnitOfWork unitOfWork,
    ILogger<Handler> logger) : ICommandHandler<Command, Result<User>>
{
    public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
    {
        try
        {
            unitOfWork.BeginTransaction();
            var user = await unitOfWork.Users.GetByIdAsync(command.Id);
            // ... operations through repository
            unitOfWork.Commit();
            return Result.Ok(user);
        }
        catch (Exception ex) { unitOfWork.Rollback(); throw; }
    }
}
```

| Check | Severity |
|-------|----------|
| Handler constructor receives ISession | Critical |
| Handler calls Session.GetAsync, SaveAsync, etc. directly | Critical |
| Application project imports NHibernate namespace | Critical |

---

## 2. Data Access Anti-Patterns (CRITICO)

### 2.1 Result.Fail in GetManyAndCount Handlers

**Severity:** CRITICO
**Impact:** GetManyAndCount handlers should rethrow exceptions, not return Result.Fail. The endpoint layer handles the exception directly via try-catch. Returning Result.Fail masks the real error.

```csharp
// BAD: GetManyAndCount returning Result.Fail
public async Task<Result<GetManyAndCountResult<UserDao>>> ExecuteAsync(
    Command command, CancellationToken ct)
{
    try
    {
        unitOfWork.BeginTransaction();
        var result = await unitOfWork.UserDaos
            .GetManyAndCountAsync(expression, command.Page, command.Size, sortingCriteria);
        unitOfWork.Commit();
        return Result.Ok(result);
    }
    catch (Exception ex)
    {
        unitOfWork.Rollback();
        return Result.Fail<GetManyAndCountResult<UserDao>>(  // VIOLATION
            new ExceptionalError(ex));
    }
}

// GOOD: GetManyAndCount rethrows
catch (Exception ex)
{
    unitOfWork.Rollback();
    logger.LogError(ex, "Error listing users");
    throw;  // Correct: rethrow for endpoint to handle
}
```

| Check | Severity |
|-------|----------|
| GetManyAndCount handler returns Result.Fail on error | Critical |
| GetManyAndCount handler missing Rollback before throw | Critical |

### 2.2 Missing Rollback After BeginTransaction

**Severity:** CRITICO
**Impact:** If a transaction is started with `BeginTransaction()` but not rolled back in the catch block, the transaction is left dangling, potentially locking database resources.

```csharp
// BAD: Missing Rollback
try
{
    unitOfWork.BeginTransaction();
    await unitOfWork.Users.CreateAsync(user);
    unitOfWork.Commit();
    return Result.Ok(user);
}
catch (Exception ex)
{
    // VIOLATION: no Rollback() — transaction left open
    logger.LogError(ex, "Error");
    return Result.Fail<User>(new ExceptionalError(ex));
}

// GOOD: Rollback in catch
catch (Exception ex)
{
    unitOfWork.Rollback();
    logger.LogError(ex, "Error creating user");
    return Result.Fail<User>(new ExceptionalError(ex));
}
```

| Check | Severity |
|-------|----------|
| BeginTransaction without corresponding Rollback in catch | Critical |
| Rollback called before BeginTransaction | Warning |

### 2.3 Missing Virtual on Entity/DAO Properties

**Severity:** CRITICO
**Impact:** NHibernate creates proxy objects for lazy loading. Without `virtual`, the proxy cannot override the property, causing runtime failures or silent data loss.

```csharp
// BAD: Non-virtual properties
public class User : AbstractDomainObject
{
    public string Email { get; set; } = string.Empty;       // VIOLATION: missing virtual
    public string Name { get; set; } = string.Empty;        // VIOLATION: missing virtual
    public IList<Role> Roles { get; set; } = new List<Role>(); // VIOLATION: missing virtual
}

// BAD: Some properties virtual, some not
public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;  // VIOLATION: inconsistent
}

// GOOD: All properties virtual
public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual IList<Role> Roles { get; set; } = new List<Role>();
}
```

| Check | Severity |
|-------|----------|
| Entity property missing `virtual` | Critical |
| DAO property missing `virtual` | Critical |
| Navigation property missing `virtual` | Critical |

### 2.4 Using Add() on Existing Entity (instead of Save())

**Severity:** CRITICO
**Impact:** `Add()` (or `SaveAsync` on a new ID) performs an INSERT. Using it on an existing entity causes `StaleObjectStateException` because the row already exists.

```csharp
// BAD: Add() on existing entity
public async Task<User> UpdateAsync(User entity)
{
    var existing = await Session.GetAsync<User>(entity.Id);
    existing.Email = entity.Email;
    await Session.SaveOrUpdateAsync(existing);  // OK but see below
    // OR:
    await Session.PersistAsync(existing);  // VIOLATION: Persist is for new entities
    return existing;
}

// GOOD: Save() for existing entities
public async Task<User> UpdateAsync(User entity)
{
    var existing = await Session.GetAsync<User>(entity.Id)
        ?? throw new ResourceNotFoundException($"User with Id {entity.Id} not found");

    existing.Email = entity.Email;
    existing.Name = entity.Name;
    existing.Validate();

    await Session.SaveAsync(existing);  // Save for existing tracked entity
    await Session.FlushAsync();
    return existing;
}
```

| Check | Severity |
|-------|----------|
| Using Add()/Persist() on existing entity | Critical |
| Mixing Add and Save patterns inconsistently | Warning |

---

## 3. Validation Anti-Patterns (IMPORTANTE)

### 3.1 Missing WithMessage() or WithErrorCode()

**Severity:** IMPORTANTE
**Impact:** Without `WithMessage()`, the client receives generic FluentValidation messages. Without `WithErrorCode()`, the client cannot programmatically identify which validation failed.

```csharp
// BAD: Missing WithErrorCode
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("The [Email] cannot be null or empty");
    // VIOLATION: missing WithErrorCode("Email")

// BAD: Missing WithMessage
RuleFor(x => x.Email)
    .NotEmpty()
    .WithErrorCode("Email");
    // VIOLATION: missing WithMessage — client gets generic text

// BAD: Missing both
RuleFor(x => x.Email)
    .NotEmpty();
    // VIOLATION: missing both WithMessage and WithErrorCode

// GOOD: Complete validation rule
RuleFor(x => x.Email)
    .NotNull()
    .NotEmpty()
    .WithMessage("The [Email] cannot be null or empty")
    .WithErrorCode("Email")
    .EmailAddress()
    .WithMessage("The [Email] is not a valid email address")
    .WithErrorCode("Email_InvalidDomain");
```

| Check | Severity |
|-------|----------|
| Validator rule missing WithMessage() | Warning |
| Validator rule missing WithErrorCode() | Warning |
| Both WithMessage and WithErrorCode missing | Warning |

### 3.2 Missing Entity GetValidator() Override

**Severity:** CRITICO
**Impact:** If the entity does not override `GetValidator()`, calling `entity.Validate()` in the repository will use the base class validator (or none), and domain validation rules will never execute.

```csharp
// BAD: Entity without GetValidator override
public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;

    public User() { }
    public User(string email) { Email = email; }
    // VIOLATION: missing GetValidator() — validation will not run
}

// GOOD: Entity with GetValidator override
public class User : AbstractDomainObject
{
    public virtual string Email { get; set; } = string.Empty;

    public User() { }
    public User(string email) { Email = email; }

    public override IValidator GetValidator() => new UserValidator();
}
```

| Check | Severity |
|-------|----------|
| Entity does not override GetValidator() | Critical |
| GetValidator() returns wrong validator type | Critical |

---

## 4. Error Handling Anti-Patterns (IMPORTANTE)

### 4.1 Wrong Exception Catch Order

**Severity:** IMPORTANTE
**Impact:** If `Exception` (generic) is caught before specific exceptions, the specific catch blocks become unreachable dead code.

```csharp
// BAD: Generic exception caught first
try
{
    // ...
}
catch (Exception ex)  // VIOLATION: catches everything — blocks below are dead code
{
    unitOfWork.Rollback();
    return Result.Fail<User>(new ExceptionalError(ex));
}
catch (InvalidDomainException ex)  // Never reached!
{
    return Result.Fail<User>(/* validation errors */);
}
catch (DuplicatedDomainException ex)  // Never reached!
{
    return Result.Fail<User>(ex.Message);
}

// GOOD: Specific exceptions first, generic last
try
{
    // ...
}
catch (InvalidDomainException ex)
{
    var errors = JsonSerializer.Deserialize<List<ValidationError>>(ex.Message);
    return Result.Fail<User>(errors.Select(e =>
        new FluentResults.Error(e.ErrorMessage)
            .WithMetadata("ErrorCode", e.ErrorCode)
            .WithMetadata("PropertyName", e.PropertyName)));
}
catch (DuplicatedDomainException ex)
{
    return Result.Fail<User>(ex.Message);
}
catch (ResourceNotFoundException ex)
{
    return Result.Fail<User>(ex.Message);
}
catch (Exception ex)
{
    unitOfWork.Rollback();
    logger.LogError(ex, "Unexpected error");
    return Result.Fail<User>(new ExceptionalError(ex));
}
```

| Check | Severity |
|-------|----------|
| Generic Exception caught before specific exceptions | Warning |
| Specific catch block unreachable after generic catch | Warning |
| Empty catch block (swallowing exceptions) | Critical |

### 4.2 Missing BeginTransaction for Write Operations

**Severity:** IMPORTANTE
**Impact:** Without `BeginTransaction()`, write operations are not atomic. If one step fails mid-operation, partial writes may persist, leaving the database in an inconsistent state.

```csharp
// BAD: Write without BeginTransaction
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    try
    {
        // VIOLATION: no BeginTransaction() — writes are not atomic
        var user = new User(command.Email, command.Name);
        await unitOfWork.Users.CreateAsync(user);
        return Result.Ok(user);
    }
    catch (Exception ex)
    {
        // No Rollback because no transaction was started
        return Result.Fail<User>(new ExceptionalError(ex));
    }
}

// GOOD: Write with proper transaction
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    try
    {
        unitOfWork.BeginTransaction();
        var user = new User(command.Email, command.Name);
        await unitOfWork.Users.CreateAsync(user);
        unitOfWork.Commit();
        return Result.Ok(user);
    }
    catch (Exception ex)
    {
        unitOfWork.Rollback();
        logger.LogError(ex, "Error creating user");
        return Result.Fail<User>(new ExceptionalError(ex));
    }
}
```

| Check | Severity |
|-------|----------|
| Write handler missing BeginTransaction | Critical |
| Commit without corresponding BeginTransaction | Warning |

---

## 5. Registration Anti-Patterns (IMPORTANTE)

### 5.1 Repository Not Registered in IUnitOfWork

**Severity:** IMPORTANTE
**Impact:** If a repository is implemented but not added to `IUnitOfWork`, handlers cannot access it. The handler will fail at compile time (if using the interface) or at runtime (if using a missing property).

```csharp
// BAD: Repository exists but not in IUnitOfWork
// File: infrastructure/repositories/ProductRepository.cs — EXISTS
// File: domain/interfaces/IProductRepository.cs — EXISTS
// File: domain/interfaces/IUnitOfWork.cs:
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    // VIOLATION: IProductRepository not registered here
    void BeginTransaction();
    void Commit();
    void Rollback();
}

// GOOD: Repository registered in both interface and implementation
// IUnitOfWork.cs
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IProductRepository Products { get; }  // Registered
    void BeginTransaction();
    void Commit();
    void Rollback();
}

// NHUnitOfWork.cs
private IProductRepository? _products;
public IProductRepository Products =>
    _products ??= new ProductRepository(_session);  // Lazy creation
```

| Check | Severity |
|-------|----------|
| Repository interface exists but not in IUnitOfWork | Critical |
| Repository in IUnitOfWork but not implemented in NHUnitOfWork | Critical |
| Repository implemented but not using lazy creation | Warning |

### 5.2 Mapper Not Registered in NHSessionFactory

**Severity:** IMPORTANTE
**Impact:** If a mapper class exists but is not registered in `NHSessionFactory`, NHibernate will not know how to map the entity/DAO. Queries will fail at runtime with mapping exceptions.

```csharp
// BAD: Mapper defined but not registered
// File: infrastructure/mappers/ProductMapper.cs — EXISTS
// File: infrastructure/NHSessionFactory.cs:
public void Configure()
{
    mapper.AddMapping<UserMapper>();
    mapper.AddMapping<UserDaoMapper>();
    // VIOLATION: ProductMapper not registered — runtime failure
}

// GOOD: All mappers registered
public void Configure()
{
    mapper.AddMapping<UserMapper>();
    mapper.AddMapping<UserDaoMapper>();
    mapper.AddMapping<ProductMapper>();     // Registered
    mapper.AddMapping<ProductDaoMapper>();  // Registered
}
```

| Check | Severity |
|-------|----------|
| Mapper class exists but not registered in NHSessionFactory | Critical |
| DAO mapper exists but not registered in NHSessionFactory | Critical |

---

## 6. Mapper Anti-Patterns (MENOR -> CRITICO)

### 6.1 Missing Mutable(false) on DAO Mappers

**Severity:** CRITICO
**Impact:** DAO mappers map to database VIEWs. Without `Mutable(false)`, NHibernate may attempt to INSERT/UPDATE/DELETE against the VIEW, which will fail at runtime.

```csharp
// BAD: DAO mapper without Mutable(false)
public class UserDaoMapper : ClassMapping<UserDao>
{
    public UserDaoMapper()
    {
        // VIOLATION: missing Mutable(false) — NHibernate may try to write to VIEW
        Schema("public");
        Table("vw_users");

        Id(x => x.Id, m => { m.Column("id"); m.Type(NHibernateUtil.Guid); });
        Property(x => x.Email, m => { m.Column("email"); });
    }
}

// GOOD: DAO mapper with Mutable(false)
public class UserDaoMapper : ClassMapping<UserDao>
{
    public UserDaoMapper()
    {
        Mutable(false);  // MANDATORY for DAO mappers
        Schema("public");
        Table("vw_users");

        Id(x => x.Id, m => { m.Column("id"); m.Type(NHibernateUtil.Guid); });
        Property(x => x.Email, m => { m.Column("email"); });
    }
}
```

| Check | Severity |
|-------|----------|
| DAO mapper missing Mutable(false) | Critical |
| DAO mapper pointing to table instead of VIEW | Critical |

---

## 7. Summary Table

| # | Anti-Pattern | Severity | Quick Description |
|---|-------------|----------|-------------------|
| 1.1 | Business Logic in Handlers | CRITICO | Validation/business rules in handler instead of domain |
| 1.2 | Persistence Logic in Domain Layer | CRITICO | ORM attributes, ISession, or persistence methods in entities |
| 1.3 | Direct ISession Usage in Handlers | CRITICO | Handler bypasses repository pattern with direct ISession |
| 2.1 | Result.Fail in GetManyAndCount | CRITICO | GetManyAndCount should throw, not return Result.Fail |
| 2.2 | Missing Rollback After BeginTransaction | CRITICO | Transaction left dangling after error |
| 2.3 | Missing Virtual on Properties | CRITICO | NHibernate proxy failure without virtual keyword |
| 2.4 | Add() on Existing Entity | CRITICO | Using Add/Persist on tracked entity causes StaleObjectStateException |
| 3.1 | Missing WithMessage/WithErrorCode | IMPORTANTE | Validator rules without error context for clients |
| 3.2 | Missing GetValidator() Override | CRITICO | Entity validation never executes |
| 4.1 | Wrong Exception Catch Order | IMPORTANTE | Generic catch before specific makes catch blocks unreachable |
| 4.2 | Missing BeginTransaction for Writes | IMPORTANTE | Write operations not atomic, risk of partial persistence |
| 5.1 | Repository Not in IUnitOfWork | IMPORTANTE | Handler cannot access repository — compile or runtime failure |
| 5.2 | Mapper Not in NHSessionFactory | IMPORTANTE | NHibernate cannot map entity/DAO — runtime failure |
| 6.1 | Missing Mutable(false) on DAO Mapper | CRITICO | NHibernate may attempt writes against VIEW |
