# Session Lifecycle

ISession lifecycle management in NHibernate: session-per-request, first-level cache, flush modes, lazy loading, detached entities, and session leak prevention.

---

## ISession Overview

```csharp
public interface ISession : IDisposable
{
    // Persistence
    void Save(object obj);
    void Update(object obj);
    void Delete(object obj);
    T Get<T>(object id);
    T Load<T>(object id);

    // Queries
    IQueryable<T> Query<T>();
    IQuery CreateQuery(string queryString);

    // Transactions
    ITransaction BeginTransaction();
    ITransaction GetCurrentTransaction();

    // Cache and flush
    void Flush();
    void Clear();
    void Evict(object obj);

    bool IsOpen { get; }
    bool IsConnected { get; }
}
```

| Responsibility | Description |
|----------------|-------------|
| **Identity Map** | One instance per entity per ID |
| **First-Level Cache** | Loaded entities cached in session |
| **Lazy Loading** | Deferred loading of associations |
| **Change Tracking** | Automatic dirty detection for UPDATE |
| **Transaction Coordination** | Manages DB transactions |

---

## Session Lifecycle States

```
ISessionFactory (Singleton, Thread-Safe)
    | OpenSession()
    v
ISession (Open, Connected) -- cache active, connection checked out
    | BeginTransaction()
    v
ISession + ITransaction (Active) -- write operations, in-memory changes
    | Commit() / Rollback()
    v
ISession (Open, no transaction) -- cache still active, can query
    | Dispose()
    v
ISession (Closed, Disposed) -- cache freed, connection returned to pool
```

---

## Session Per Request Pattern

One ISession per HTTP request, managed via DI scoped lifetime.

```csharp
public static IServiceCollection ConfigureUnitOfWork(
    this IServiceCollection services, IConfiguration configuration)
{
    string connectionString = ConnectionStringBuilder.BuildPostgresConnectionString();
    var factory = new NHSessionFactory(connectionString);
    var sessionFactory = factory.BuildNHibernateSessionFactory();

    // SessionFactory: Singleton (thread-safe)
    services.AddSingleton(sessionFactory);

    // ISession: Scoped (one per HTTP request)
    services.AddScoped(factory => sessionFactory.OpenSession());

    // IUnitOfWork: Scoped (wraps ISession)
    services.AddScoped<IUnitOfWork, NHUnitOfWork>();

    return services;
}
```

Request flow:
1. HTTP request arrives -> DI creates scope -> `OpenSession()` executes
2. Handler receives `IUnitOfWork` -> `BeginTransaction()` -> business operations -> `Commit()`/`Rollback()`
3. Scope disposes at end of request -> `ISession.Dispose()` -> connection returned to pool

---

## First-Level Cache (Identity Map)

```csharp
using (var session = sessionFactory.OpenSession())
{
    var user1 = session.Get<User>(userId);  // SELECT executed
    var user2 = session.Get<User>(userId);  // Returns from cache, NO SQL

    Assert.True(Object.ReferenceEquals(user1, user2));  // Same instance
}
```

### Clear() -- Remove All from Cache

```csharp
session.Flush();  // Write pending changes to DB
session.Clear();  // Free all cached entities (they become DETACHED)
```

Use for batch processing to prevent `OutOfMemoryException`:

```csharp
for (int i = 0; i < 10000; i++)
{
    session.Save(new User($"user{i}@example.com", $"User {i}"));
    if (i % 100 == 0)
    {
        session.Flush();
        session.Clear();
    }
}
```

Never call `Clear()` inside a transaction with unflushed changes -- those changes will be lost.

### Evict() -- Remove One Entity

```csharp
session.Evict(user1);  // Only user1 becomes DETACHED
var user3 = session.Get<User>(userId);  // New SELECT, new instance
```

---

## Flush Modes

| FlushMode | Behavior | Use |
|-----------|----------|-----|
| **Auto** | Flush before queries and on Commit | Default |
| **Commit** | Flush only on Commit | Optimization |
| **Always** | Flush before every query | Debugging |
| **Manual** | Flush only on explicit call | Full control |

### FlushWhenNotActiveTransaction Pattern

```csharp
protected internal void FlushWhenNotActiveTransaction()
{
    var currentTransaction = this._session.GetCurrentTransaction();
    if (currentTransaction == null || !currentTransaction.IsActive)
        this._session.Flush();
}
```

- **With transaction**: No immediate flush. `Commit()` handles it.
- **Without transaction**: Immediate flush for isolated operations.

Used in repository `Add`/`Update`/`Delete` methods to work correctly in both scenarios.

---

## Threading and Concurrency

**ISession is NOT thread-safe.** Never share across threads.

```csharp
// WRONG: shared session across threads
await Task.WhenAll(
    Task.Run(() => _session.Get<User>(id1)),
    Task.Run(() => _session.Get<User>(id2))
);

// CORRECT: one session per request (Scoped DI)
public class GoodService(IUnitOfWork uoW)
{
    public async Task SequentialWork()
    {
        var user1 = await _uoW.Users.GetByIdAsync(userId1);
        var user2 = await _uoW.Users.GetByIdAsync(userId2);
    }
}
```

**ISessionFactory IS thread-safe** -- register as Singleton. For parallel processing, open a new session per thread:

```csharp
await Parallel.ForEachAsync(userIds, async (userId, ct) =>
{
    using (var session = sessionFactory.OpenSession())
    using (var transaction = session.BeginTransaction())
    {
        var user = await session.GetAsync<User>(userId, ct);
        user.Name = $"Processed {user.Name}";
        await session.UpdateAsync(user, ct);
        await transaction.CommitAsync(ct);
    }
});
```

---

## LazyInitializationException

Occurs when accessing a lazy-loaded association after the session that loaded the entity has been closed.

```csharp
User user;
using (var session = sessionFactory.OpenSession())
{
    user = session.Get<User>(userId);
}  // Session closed, user is DETACHED

var roles = user.Roles;  // LazyInitializationException!
```

### Solutions

**1. Eager Loading (Fetch)**

```csharp
var user = await session.Query<User>()
    .Where(u => u.Id == userId)
    .Fetch(u => u.Roles)
    .SingleOrDefaultAsync();
// user.Roles already loaded via JOIN
```

**2. Session Per Request** -- session stays open for entire handler

```csharp
public class Handler(IUnitOfWork uoW)
{
    public async Task<UserDto> Execute(Query query)
    {
        var user = await _uoW.Users.GetByIdAsync(query.UserId);
        var roleNames = user.Roles.Select(r => r.Name).ToList();  // Works
        return new UserDto(user, roleNames);
    }
}
```

**3. DTO Projection** -- project before closing session

```csharp
var dto = await session.Query<User>()
    .Where(u => u.Id == userId)
    .Select(u => new UserDto
    {
        Id = u.Id,
        Email = u.Email,
        RoleNames = u.Roles.Select(r => r.Name).ToList()  // Executes in DB
    })
    .SingleOrDefaultAsync();
```

---

## Entity States

```
TRANSIENT  --session.Save()-->  PERSISTENT  --session.Dispose()/Evict()-->  DETACHED
   (new, no ID, not tracked)        (has ID, in Identity Map, tracked)         (has ID, not tracked)
                                                                                |
                                PERSISTENT  <--session.Update()/Merge()--------+
```

### Reattaching Detached Entities

**Update()** -- reattaches the same instance:

```csharp
session2.Update(user);  // user becomes PERSISTENT in session2
transaction.Commit();   // UPDATE executed
```

**Merge()** -- returns a new PERSISTENT copy, original stays detached:

```csharp
var mergedUser = session2.Merge(user);  // mergedUser is PERSISTENT
// user is still DETACHED
transaction.Commit();
```

---

## Session Leak Prevention

A session leak = `ISession` not disposed, leaving connection open in pool.

Consequences: connection pool exhaustion, memory leaks, application hangs.

```csharp
// WRONG: no Dispose
var session = sessionFactory.OpenSession();
var user = session.Get<User>(userId);
// Connection stays open

// CORRECT: using statement
using (var session = sessionFactory.OpenSession())
{
    var user = session.Get<User>(userId);
}  // Dispose() called automatically

// BEST: DI with Scoped lifetime
services.AddScoped(factory => sessionFactory.OpenSession());
```

### NHUnitOfWork Dispose Pattern

```csharp
public class NHUnitOfWork : IUnitOfWork
{
    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
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

    ~NHUnitOfWork() { Dispose(false); }
}
```

---

## Anti-Patterns

- **Singleton ISession**: use Scoped, never `services.AddSingleton(sessionFactory.OpenSession())`
- **Sharing ISession across threads**: one session per request
- **Clear() with unflushed changes in a transaction**: always Flush before Clear
- **Accessing lazy associations outside session**: use Fetch, DTO projection, or session-per-request
- **Keeping session open during slow I/O**: close session before email/file operations, reopen after
