# Unit of Work Reference

Complete reference for the NHUnitOfWork implementation in APSYS backend projects.

---

## Full NHUnitOfWork Implementation

```csharp
using {project}.domain.interfaces.repositories;
using NHibernate;

namespace {project}.infrastructure.nhibernate;

/// <summary>
/// NHUnitOfWork is a concrete implementation of the IUnitOfWork interface.
/// It manages transactions and the lifecycle of database operations
/// in an NHibernate context.
/// </summary>
public class NHUnitOfWork : IUnitOfWork
{
    private bool _disposed = false;
    protected internal readonly ISession _session;
    protected internal readonly IServiceProvider _serviceProvider;
    protected internal ITransaction? _transaction;

    #region crud Repositories

    // CRUD repos: need IServiceProvider for FluentValidation validators
    public IRoleRepository Roles => new NHRoleRepository(_session, _serviceProvider);
    public IUserRepository Users => new NHUserRepository(_session, _serviceProvider);
    public IPrototypeRepository Prototypes => new NHPrototypeRepository(_session, _serviceProvider);
    public ITechnicalStandardRepository TechnicalStandards
        => new NHTechnicalStandardRepository(_session, _serviceProvider);

    #endregion

    #region read-only Repositories

    // Read-only repos: NO IServiceProvider needed (no validation)
    public ITechnicalStandardDaoRepository TechnicalStandardDaos
        => new NHTechnicalStandardDaoRepository(_session);
    public IPrototypeDaoRepository PrototypeDaos
        => new NHPrototypeDaoRepository(_session);

    #endregion

    public NHUnitOfWork(ISession session, IServiceProvider serviceProvider)
    {
        _session = session;
        _serviceProvider = serviceProvider;
    }

    public void BeginTransaction()
    {
        this._transaction = this._session.BeginTransaction();
    }

    public void Commit()
    {
        if (_transaction != null && _transaction.IsActive)
            _transaction.Commit();
        else
            throw new TransactionException("The actual transaction is not longer active");
    }

    public bool IsActiveTransaction()
        => _transaction != null && _transaction.IsActive;

    public void ResetTransaction()
        => _transaction = _session.BeginTransaction();

    public void Rollback()
    {
        if (_transaction != null)
        {
            _transaction.Rollback();
        }
        else
            throw new ArgumentNullException(
                $"No active exception found for session {_session.Connection.ConnectionString}");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Order matters: transaction first, then session
                if (this._transaction != null)
                    this._transaction.Dispose();
                this._session.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~NHUnitOfWork()
    {
        Dispose(false);
    }
}
```

---

## Lazy Repository Creation (Property Pattern)

Repositories are created on-demand via property accessors, not in the constructor:

```csharp
// CORRECT: Lazy creation -- only instantiated when accessed
public IOrderRepository Orders => new NHOrderRepository(_session, _serviceProvider);

// WRONG: Eager creation -- all repos instantiated even if unused
private readonly IOrderRepository _orders;
public IOrderRepository Orders => _orders;  // Created in constructor
```

**Trade-off**: A new instance is created each property access. Do not cache repository references across method calls.

---

## CRUD Repos vs Read-Only Repos

| | CRUD Repository | Read-Only Repository |
|---|---|---|
| **Base class** | `NHRepository<T, TKey>` | `NHReadOnlyRepository<T, TKey>` |
| **Constructor args** | `ISession` + `IServiceProvider` | `ISession` only |
| **Why IServiceProvider?** | Resolves `AbstractValidator<T>` from DI | No validation needed |
| **UoW property** | `=> new NHXRepo(_session, _serviceProvider)` | `=> new NHXDaoRepo(_session)` |

---

## Transaction Methods

| Method | Purpose | Throws when |
|--------|---------|-------------|
| `BeginTransaction()` | Starts a new DB transaction | N/A |
| `Commit()` | Flushes + commits changes | No active transaction |
| `Rollback()` | Reverts all changes | No transaction exists |
| `ResetTransaction()` | Starts a new transaction (after rollback) | N/A |
| `IsActiveTransaction()` | Checks if transaction is active | N/A (returns bool) |

---

## Dispose Pattern

**Dispose order is critical**: transaction first, then session.

```csharp
protected virtual void Dispose(bool disposing)
{
    if (!_disposed)
    {
        if (disposing)
        {
            // 1. Transaction first (depends on session)
            if (this._transaction != null)
                this._transaction.Dispose();
            // 2. Session second
            this._session.Dispose();
        }
        _disposed = true;  // Prevents double-dispose
    }
}
```

- `_disposed` flag prevents double-dispose
- `GC.SuppressFinalize(this)` skips finalizer when `Dispose()` is called explicitly
- Finalizer `~NHUnitOfWork()` is a safety net if `Dispose()` is forgotten

---

## Adding a New Repository (Step-by-Step)

### 1. Add interface property to `IUnitOfWork` (domain layer)

```csharp
public interface IUnitOfWork : IDisposable
{
    // Existing repos...

    // NEW: Add property for the new repository
    IOrderRepository Orders { get; }
    // or for read-only:
    IReadOnlyRepository<OrderDao, Guid> OrderDaos { get; }

    // Transaction methods...
}
```

### 2. Add lazy property to `NHUnitOfWork` (infrastructure layer)

```csharp
// For CRUD repository:
public IOrderRepository Orders
    => new NHOrderRepository(_session, _serviceProvider);

// For read-only repository:
public IReadOnlyRepository<OrderDao, Guid> OrderDaos
    => new NHReadOnlyRepository<OrderDao, Guid>(_session);
```

### 3. Register validator (CRUD repos only)

```csharp
// In ConfigureValidators():
services.AddScoped<AbstractValidator<Order>, OrderValidator>();
```
