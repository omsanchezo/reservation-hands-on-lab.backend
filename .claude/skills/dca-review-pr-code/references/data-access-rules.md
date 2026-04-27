# Data Access Rules Reference

Comprehensive rules for validating data access patterns in APSYS .NET backend projects.

---

## 1. Repository Types

Choose the repository type based on the operation:

| Operation Type | Interface | Use Case |
|---------------|-----------|----------|
| CRUD (write) | `IRepository<T, Guid>` | Create, Update, Delete entities with transactions |
| Read-only queries | `IReadOnlyRepository<T, Guid>` | Paginated lists, search, reporting via DAOs |

```csharp
// GOOD: Write repository interface in Domain
namespace project.domain.interfaces;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email);
}

// GOOD: Read-only repository interface in Domain
public interface IUserDaoRepository : IReadOnlyRepository<UserDao, Guid>
{
}
```

```csharp
// BAD: Using IRepository for read-only operations
public interface IUserDaoRepository : IRepository<UserDao, Guid>  // VIOLATION: DAO is read-only
{
}

// BAD: Using IReadOnlyRepository for entities that need CRUD
public interface IUserRepository : IReadOnlyRepository<User, Guid>  // VIOLATION: cannot write
{
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| CRUD entity using IReadOnlyRepository | Critical |
| DAO using IRepository instead of IReadOnlyRepository | Critical |
| Repository interface not in domain/interfaces/ | Critical |
| Repository not registered in IUnitOfWork | Critical |

---

## 2. Repository Implementation Patterns

### CreateAsync Pattern

Validate the entity, check for duplicates, then persist:

```csharp
// GOOD: CreateAsync implementation
public async Task<User> CreateAsync(User entity)
{
    entity.Validate();  // Throws InvalidDomainException if invalid

    var existing = await Session.Query<User>()
        .Where(u => u.Email.ToLowerInvariant() == entity.Email.ToLowerInvariant())
        .SingleOrDefaultAsync();

    if (existing != null)
        throw new DuplicatedDomainException($"User with email {entity.Email} already exists");

    await Session.SaveAsync(entity);
    await Session.FlushAsync();
    return entity;
}
```

```csharp
// BAD: Missing validation before persist
public async Task<User> CreateAsync(User entity)
{
    await Session.SaveAsync(entity);  // VIOLATION: no Validate() call
    return entity;
}

// BAD: Missing FlushAsync after write
public async Task<User> CreateAsync(User entity)
{
    entity.Validate();
    await Session.SaveAsync(entity);
    // VIOLATION: missing FlushAsync()
    return entity;
}

// BAD: Case-sensitive duplicate check
var existing = await Session.Query<User>()
    .Where(u => u.Email == entity.Email)  // VIOLATION: case-sensitive
    .SingleOrDefaultAsync();
```

### UpdateAsync Pattern

Get existing entity, check duplicates (excluding self), update properties, validate, persist:

```csharp
// GOOD: UpdateAsync implementation
public async Task<User> UpdateAsync(User entity)
{
    var existing = await Session.GetAsync<User>(entity.Id)
        ?? throw new ResourceNotFoundException($"User with Id {entity.Id} not found");

    var duplicate = await Session.Query<User>()
        .Where(u => u.Email.ToLowerInvariant() == entity.Email.ToLowerInvariant()
                  && u.Id != entity.Id)  // Exclude self
        .SingleOrDefaultAsync();

    if (duplicate != null)
        throw new DuplicatedDomainException($"User with email {entity.Email} already exists");

    existing.Email = entity.Email;
    existing.Name = entity.Name;
    existing.Validate();

    await Session.SaveAsync(existing);
    await Session.FlushAsync();
    return existing;
}
```

```csharp
// BAD: Duplicate check without excluding self
var duplicate = await Session.Query<User>()
    .Where(u => u.Email.ToLowerInvariant() == entity.Email.ToLowerInvariant())
    // VIOLATION: missing && u.Id != entity.Id — entity always matches itself
    .SingleOrDefaultAsync();
```

### GetByXXXAsync Pattern

Always guard against null with `ResourceNotFoundException`:

```csharp
// GOOD: Null guard with ResourceNotFoundException
public async Task<User> GetByIdAsync(Guid id)
{
    return await Session.GetAsync<User>(id)
        ?? throw new ResourceNotFoundException($"User with Id {id} not found");
}
```

```csharp
// BAD: Returning null instead of throwing
public async Task<User?> GetByIdAsync(Guid id)
{
    return await Session.GetAsync<User>(id);  // VIOLATION: caller must handle null
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| CreateAsync missing entity.Validate() | Critical |
| UpdateAsync missing duplicate check excluding self | Critical |
| GetByXXXAsync returning null instead of throwing | Critical |
| Duplicate check is case-sensitive (missing ToLowerInvariant) | Warning |
| Missing FlushAsync() after write operations | Critical |
| Using Add() on existing entity instead of Save() | Critical |

---

## 3. NHibernate Entity Mappers

### ClassMapping Configuration

Follow this configuration order: Schema -> Table -> Id -> Properties -> Relations.

```csharp
// GOOD: Entity mapper
namespace project.infrastructure.mappers;

public class UserMapper : ClassMapping<User>
{
    public UserMapper()
    {
        Schema("public");
        Table("users");

        Id(x => x.Id, m =>
        {
            m.Column("id");
            m.Type(NHibernateUtil.Guid);
            m.Generator(Generators.GuidComb);
        });

        Property(x => x.Email, m =>
        {
            m.Column("email");
            m.Type(NHibernateUtil.String);
            m.NotNullable(true);
            m.Unique(true);
            m.Length(255);
        });

        Property(x => x.Name, m =>
        {
            m.Column("name");
            m.Type(NHibernateUtil.String);
            m.NotNullable(true);
            m.Length(100);
        });

        Property(x => x.CreationDate, m =>
        {
            m.Column("creation_date");
            m.Type(NHibernateUtil.DateTime);
            m.NotNullable(true);
        });

        Property(x => x.Locked, m =>
        {
            m.Column("locked");
            m.Type(NHibernateUtil.Boolean);
        });

        // Relations
        ManyToOne(x => x.Department, m =>
        {
            m.Column("department_id");
            m.NotNullable(true);
            m.Cascade(Cascade.None);
        });

        Bag(x => x.Roles, m =>
        {
            m.Key(k => k.Column("user_id"));
            m.Cascade(Cascade.All | Cascade.DeleteOrphans);
            m.Lazy(CollectionLazy.Lazy);
        }, r => r.OneToMany());
    }
}
```

### NHibernateUtil Type Mapping

| C# Type | NHibernateUtil | Column Example |
|---------|---------------|----------------|
| `string` | `NHibernateUtil.String` | `m.Length(255)` |
| `int` | `NHibernateUtil.Int32` | |
| `long` | `NHibernateUtil.Int64` | |
| `decimal` | `NHibernateUtil.Decimal` | |
| `bool` | `NHibernateUtil.Boolean` | |
| `DateTime` | `NHibernateUtil.DateTime` | |
| `Guid` | `NHibernateUtil.Guid` | |

### Property Restrictions

| Restriction | Usage | Example |
|-------------|-------|---------|
| `NotNullable(true)` | Required fields | `m.NotNullable(true)` |
| `Unique(true)` | Unique constraints | `m.Unique(true)` |
| `Length(n)` | String max length | `m.Length(255)` |

### Relationships

| Type | When | Configuration |
|------|------|---------------|
| `ManyToOne` | Entity references another entity | `Cascade(Cascade.None)` typically |
| `OneToMany` (via Bag) | Entity owns a collection | `Cascade(Cascade.All \| Cascade.DeleteOrphans)` |
| `ManyToMany` (via Bag) | Join table | Explicit `Table()` and `Key` |

### Mapper Registration

Every mapper must be registered in `NHSessionFactory`:

```csharp
// In NHSessionFactory configuration
mapper.AddMapping<UserMapper>();
mapper.AddMapping<UserDaoMapper>();
```

```csharp
// BAD: Mapper defined but not registered in NHSessionFactory
// Entity/DAO will not be mapped — queries will fail at runtime
```

### Review Checks

| Check | Severity |
|-------|----------|
| Mapper not registered in NHSessionFactory | Critical |
| Configuration order not followed (Schema/Table/Id/Properties/Relations) | Warning |
| NHibernateUtil type mismatch with C# type | Critical |
| Missing NotNullable on required field | Warning |
| Wrong cascade configuration for relationship | Critical |
| Column name not in snake_case | Warning |

---

## 4. DAO Mappers

DAOs (Data Access Objects) are read-only POCOs that map to database VIEWs for listing and search operations.

### Requirements

- `Mutable(false)` is **mandatory** (DAO maps to a VIEW, not a table)
- Maps to a database VIEW (not a table)
- `SearchAll` property uses `CONCAT_WS` formula for full-text-like search
- No navigation properties or relationships
- All properties must be `virtual` (NHibernate requirement)

```csharp
// GOOD: DAO mapper
public class UserDaoMapper : ClassMapping<UserDao>
{
    public UserDaoMapper()
    {
        Mutable(false);  // MANDATORY for DAOs
        Schema("public");
        Table("vw_users");  // Maps to VIEW

        Id(x => x.Id, m =>
        {
            m.Column("id");
            m.Type(NHibernateUtil.Guid);
        });

        Property(x => x.Email, m =>
        {
            m.Column("email");
            m.Type(NHibernateUtil.String);
        });

        Property(x => x.Name, m =>
        {
            m.Column("name");
            m.Type(NHibernateUtil.String);
        });

        Property(x => x.DepartmentName, m =>
        {
            m.Column("department_name");
            m.Type(NHibernateUtil.String);
        });

        Property(x => x.SearchAll, m =>
        {
            m.Formula("CONCAT_WS(' ', email, name, department_name)");
            m.Type(NHibernateUtil.String);
        });
    }
}
```

```csharp
// BAD: Missing Mutable(false) on DAO mapper
public class UserDaoMapper : ClassMapping<UserDao>
{
    public UserDaoMapper()
    {
        // VIOLATION: Missing Mutable(false) — NHibernate may try to write to a VIEW
        Schema("public");
        Table("vw_users");
        // ...
    }
}

// BAD: DAO mapper with navigation properties
ManyToOne(x => x.Department, m =>  // VIOLATION: DAOs should not have relationships
{
    m.Column("department_id");
});

// BAD: DAO mapper pointing to a table instead of a VIEW
Table("users");  // VIOLATION: should be "vw_users" — DAOs map to VIEWs
```

### Review Checks

| Check | Severity |
|-------|----------|
| DAO mapper missing `Mutable(false)` | Critical |
| DAO mapper not pointing to a VIEW | Critical |
| DAO mapper with navigation properties/relationships | Warning |
| DAO missing SearchAll with CONCAT_WS formula | Warning |
| DAO property missing `virtual` keyword | Critical |

---

## 5. Unit of Work Pattern

### Lazy Repository Creation

Repositories are created lazily via properties in the Unit of Work:

```csharp
// GOOD: Unit of Work with lazy repository creation
public class NHUnitOfWork : IUnitOfWork
{
    private readonly ISession _session;
    private ITransaction? _transaction;

    private IUserRepository? _users;
    private IUserDaoRepository? _userDaos;

    public NHUnitOfWork(ISession session)
    {
        _session = session;
    }

    public IUserRepository Users =>
        _users ??= new UserRepository(_session);

    public IUserDaoRepository UserDaos =>
        _userDaos ??= new UserDaoRepository(_session);

    public void BeginTransaction()
    {
        _transaction = _session.BeginTransaction();
    }

    public void Commit()
    {
        _transaction?.Commit();
    }

    public void Rollback()
    {
        _transaction?.Rollback();
    }

    public void Dispose()
    {
        // Transaction first, then session
        _transaction?.Dispose();
        _session?.Dispose();
    }
}
```

```csharp
// BAD: Eager repository creation
public NHUnitOfWork(ISession session)
{
    _session = session;
    _users = new UserRepository(_session);  // VIOLATION: eager creation — wasteful
    _userDaos = new UserDaoRepository(_session);
}

// BAD: Wrong Dispose order
public void Dispose()
{
    _session?.Dispose();       // VIOLATION: session disposed before transaction
    _transaction?.Dispose();
}
```

### Repository Registration

Every new repository must be:
1. Declared as a property in `IUnitOfWork` interface
2. Implemented with lazy creation in `NHUnitOfWork`

```csharp
// In IUnitOfWork interface
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IUserDaoRepository UserDaos { get; }
    // New repositories added here
    void BeginTransaction();
    void Commit();
    void Rollback();
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Repository not registered in IUnitOfWork | Critical |
| Eager repository creation instead of lazy | Warning |
| Wrong Dispose order (session before transaction) | Critical |
| Missing repository property in IUnitOfWork interface | Critical |

---

## 6. Transaction Management

### Write Operation Pattern

Every write operation follows: `BeginTransaction` -> operation -> `Commit` on success, `Rollback` in catch.

```csharp
// GOOD: Correct transaction management
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    try
    {
        unitOfWork.BeginTransaction();

        var user = new User(command.Email, command.Name);
        await unitOfWork.Users.CreateAsync(user);
        await unitOfWork.FlushWhenNotActiveTransaction();

        unitOfWork.Commit();
        return Result.Ok(user);
    }
    catch (InvalidDomainException ex)
    {
        return Result.Fail<User>(/* validation errors */);
    }
    catch (DuplicatedDomainException ex)
    {
        return Result.Fail<User>(ex.Message);
    }
    catch (Exception ex)
    {
        unitOfWork.Rollback();
        logger.LogError(ex, "Error creating user");
        return Result.Fail<User>(new ExceptionalError(ex));
    }
}
```

```csharp
// BAD: Missing BeginTransaction for write operation
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    try
    {
        // VIOLATION: no BeginTransaction() — writes won't be atomic
        var user = new User(command.Email, command.Name);
        await unitOfWork.Users.CreateAsync(user);
        return Result.Ok(user);
    }
    catch (Exception ex) { /* ... */ }
}

// BAD: Missing Rollback in catch block
catch (Exception ex)
{
    logger.LogError(ex, "Error");
    // VIOLATION: missing unitOfWork.Rollback() — transaction left dangling
    return Result.Fail<User>(new ExceptionalError(ex));
}
```

### Flush Patterns

| Method | When to Use |
|--------|------------|
| `FlushAsync()` | After write operations (SaveAsync, DeleteAsync) to push changes to DB |
| `FlushWhenNotActiveTransaction()` | When you want to flush only if no active transaction is managing it |

```csharp
// GOOD: FlushAsync after write
await Session.SaveAsync(entity);
await Session.FlushAsync();

// BAD: No flush after write
await Session.SaveAsync(entity);
// VIOLATION: changes may not be persisted — stays in NHibernate session cache only
```

### GetManyAndCount Transaction Pattern

GetManyAndCount handlers use a different error pattern: `Rollback` + `throw` (NOT `Result.Fail`):

```csharp
// GOOD: GetManyAndCount error handling
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
        logger.LogError(ex, "Error listing users");
        throw;  // RETHROW — do NOT return Result.Fail
    }
}
```

```csharp
// BAD: GetManyAndCount returning Result.Fail
catch (Exception ex)
{
    unitOfWork.Rollback();
    return Result.Fail<GetManyAndCountResult<UserDao>>(  // VIOLATION: should throw
        new ExceptionalError(ex));
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Missing BeginTransaction for write operations | Critical |
| Missing Rollback in catch block after BeginTransaction | Critical |
| Missing FlushAsync after write operations | Critical |
| GetManyAndCount returning Result.Fail instead of throw | Critical |
| Commit without corresponding BeginTransaction | Warning |

---

## 7. Query Patterns

### LINQ to NHibernate

Use NHibernate LINQ API for queries:

```csharp
// GOOD: LINQ query with NHibernate
var user = await Session.Query<User>()
    .Where(u => u.Email.ToLowerInvariant() == email.ToLowerInvariant())
    .SingleOrDefaultAsync();
```

### Case-Insensitive Comparisons

Always use `ToLowerInvariant()` for case-insensitive string comparisons:

```csharp
// GOOD: Case-insensitive comparison
.Where(u => u.Email.ToLowerInvariant() == email.ToLowerInvariant())

// BAD: Case-sensitive comparison
.Where(u => u.Email == email)  // VIOLATION: won't match "user@test.com" vs "User@test.com"

// BAD: Using ToLower() instead of ToLowerInvariant()
.Where(u => u.Email.ToLower() == email.ToLower())  // VIOLATION: culture-dependent
```

### SingleOrDefaultAsync vs FirstOrDefaultAsync

| Method | When to Use |
|--------|------------|
| `SingleOrDefaultAsync` | When 0 or 1 result is expected (unique fields: email, code) |
| `FirstOrDefaultAsync` | When multiple results are possible and you want the first |

```csharp
// GOOD: Unique field lookup
var user = await Session.Query<User>()
    .Where(u => u.Email.ToLowerInvariant() == email.ToLowerInvariant())
    .SingleOrDefaultAsync();  // Email is unique — 0 or 1 result

// GOOD: Non-unique field lookup
var firstActive = await Session.Query<User>()
    .Where(u => u.Status == "Active")
    .FirstOrDefaultAsync();  // Multiple active users may exist
```

### GetManyAndCount with SortingCriteria

Paginated queries use `GetManyAndCountAsync` with `SortingCriteria`:

```csharp
// GOOD: Paginated query with sorting
var sortingCriteria = new SortingCriteria(command.SortBy, command.SortDirection);
var result = await unitOfWork.UserDaos
    .GetManyAndCountAsync(expression, command.Page, command.Size, sortingCriteria);
```

### N+1 Query Prevention

Avoid N+1 queries by using eager loading or separate queries:

```csharp
// BAD: N+1 query — loading collection for each entity in a loop
var users = await Session.Query<User>().ToListAsync();
foreach (var user in users)
{
    var roles = user.Roles;  // VIOLATION: triggers lazy load query per user
    Console.WriteLine($"{user.Name}: {roles.Count} roles");
}

// GOOD: Eager loading with Fetch
var users = await Session.Query<User>()
    .Fetch(u => u.Roles)  // Eager load in a single query
    .ToListAsync();

// GOOD: Separate query for related data
var users = await Session.Query<User>().ToListAsync();
var userIds = users.Select(u => u.Id).ToList();
var roles = await Session.Query<Role>()
    .Where(r => userIds.Contains(r.UserId))
    .ToListAsync();
```

### Async/Await Best Practices

All data access operations must use async methods and propagate `CancellationToken`:

```csharp
// GOOD: Async with CancellationToken
public async Task<User> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    return await Session.GetAsync<User>(id, ct)
        ?? throw new ResourceNotFoundException($"User with Id {id} not found");
}

// GOOD: Handler propagates CancellationToken
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    var user = await unitOfWork.Users.GetByIdAsync(command.Id, ct);
    // ...
}
```

```csharp
// BAD: Blocking call instead of async
public User GetById(Guid id)
{
    return Session.Get<User>(id);  // VIOLATION: blocks the thread
}

// BAD: Async method missing CancellationToken parameter
public async Task<User> GetByIdAsync(Guid id)
{
    return await Session.GetAsync<User>(id);  // VIOLATION: cannot be cancelled
}

// BAD: Not awaiting async call
public Task<User> GetByIdAsync(Guid id, CancellationToken ct)
{
    return Session.GetAsync<User>(id, ct);  // VIOLATION: exceptions won't propagate correctly
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Case-sensitive string comparison in query | Warning |
| Using ToLower() instead of ToLowerInvariant() | Warning |
| SingleOrDefaultAsync on non-unique field | Warning |
| Potential N+1 query (lazy loading in loop) | Warning |
| Raw SQL without justification | Warning |
| Missing SortingCriteria in GetManyAndCount | Warning |
| Blocking call (.Result, .Wait(), .GetAwaiter().GetResult()) | Warning |
| Async method missing CancellationToken parameter | Suggestion |
| CancellationToken not propagated to inner async calls | Suggestion |
