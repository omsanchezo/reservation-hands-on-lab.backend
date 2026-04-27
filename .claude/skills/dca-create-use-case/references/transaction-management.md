# Transaction Management

ACID properties, isolation levels, transaction lifecycle, try-catch-rollback patterns, flush vs commit, deadlock prevention, and anti-patterns.

---

## ACID Properties

| Property | Guarantee |
|----------|-----------|
| **Atomicity** | All operations complete or none do |
| **Consistency** | DB moves from valid state to valid state |
| **Isolation** | Concurrent transactions don't see each other's partial changes |
| **Durability** | Committed changes persist through system failures |

```csharp
unitOfWork.BeginTransaction();
try
{
    await unitOfWork.Users.CreateAsync(email, name);     // Operation 1
    await unitOfWork.Roles.AssignToUserAsync(userId);     // Operation 2
    await unitOfWork.Logs.CreateAsync("User created");    // Operation 3
    unitOfWork.Commit();  // All 3 execute
}
catch
{
    unitOfWork.Rollback(); // All 3 reversed
    throw;
}
```

---

## Isolation Levels

| Level | Dirty Read | Non-Repeatable Read | Phantom Read | Performance |
|-------|-----------|--------------------|--------------| ------------|
| **Read Uncommitted** | Allowed | Allowed | Allowed | Fastest |
| **Read Committed** | Prevented | Allowed | Allowed | Fast |
| **Repeatable Read** | Prevented | Prevented | Allowed | Slow |
| **Serializable** | Prevented | Prevented | Prevented | Slowest |

```csharp
// Specify isolation level when beginning transaction
public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
{
    this._transaction = this._session.BeginTransaction(isolationLevel);
}
```

**Guidelines:**
- **Read Committed** (default): Standard CRUD operations
- **Repeatable Read**: Financial calculations, critical reports
- **Serializable**: Inventory, accounting (caution: can cause deadlocks)
- **Read Uncommitted**: Non-critical reports only

---

## Transaction Lifecycle

```
BeginTransaction() --> ACTIVE --> Commit() --> COMMITTED
                         |
                         +--> Rollback() --> ROLLED BACK
                         |
                         +--> Error --> FAILED
```

| Method | Description |
|--------|-------------|
| `BeginTransaction()` | Start new transaction |
| `Commit()` | Confirm changes to DB |
| `Rollback()` | Undo all changes |
| `IsActiveTransaction()` | Check if transaction is active |
| `Dispose()` | Release transaction resources |

---

## Try-Catch-Rollback Pattern (Standard)

The primary pattern used in use cases:

```csharp
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    _unitOfWork.BeginTransaction();

    try
    {
        var user = await _unitOfWork.Users.CreateAsync(command.Email, command.Name);
        _unitOfWork.Commit();
        return Result.Ok(user);
    }
    catch (InvalidDomainException idex)
    {
        _unitOfWork.Rollback();
        var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
        var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid data";
        return Result.Fail(new Error(firstErrorMessage)
            .CausedBy(idex)
            .WithMetadata("ValidationErrors", idex));
    }
    catch (DuplicatedDomainException ddex)
    {
        _unitOfWork.Rollback();
        return Result.Fail(new Error(ddex.Message).CausedBy(ddex));
    }
    catch (Exception ex)
    {
        _unitOfWork.Rollback();
        return Result.Fail(new Error(ex.Message).CausedBy(ex));
    }
}
```

**Key rules:**
1. `BeginTransaction()` at the start
2. `try` wraps all operations
3. `Commit()` inside try on success
4. `Rollback()` in EVERY catch block
5. Catch exceptions from most specific to most generic

### Exception Types

| Exception | Action |
|-----------|--------|
| `InvalidDomainException` | Rollback + return validation errors |
| `DuplicatedDomainException` | Rollback + return duplicate error |
| `NotFoundException` | Rollback + return 404 |
| `HttpRequestException` | Rollback + compensate external service |
| `Exception` | Rollback + log + return generic error |

---

## Compensating Transaction Pattern

For operations involving external services:

```csharp
_unitOfWork.BeginTransaction();
try
{
    var auth0User = await _identityService.CreateAsync(email, name);
    var user = await _unitOfWork.Users.CreateAsync(email, name);
    _unitOfWork.Commit();
    return Result.Ok(user);
}
catch (Exception ex)
{
    _unitOfWork.Rollback();
    // COMPENSATE: undo external service operation
    try { await _identityService.DeleteAsync(email); }
    catch (Exception compEx) { _logger.LogError(compEx, "Failed to compensate Auth0 user"); }
    return Result.Fail(ex.Message);
}
```

---

## Flush vs Commit

| Operation | Executes SQL | Confirms Transaction | Can Rollback After |
|-----------|-------------|---------------------|-------------------|
| `Flush()` | Yes | No | Yes |
| `Commit()` | Yes (implicitly) | Yes | No |

```csharp
_unitOfWork.BeginTransaction();
try
{
    var user = new User("test@example.com");
    await _unitOfWork.Users.AddAsync(user);

    _session.Flush();
    // SQL executed (INSERT), but transaction still active
    // Can still Rollback

    var userId = user.Id;  // ID generated by DB

    _unitOfWork.Commit();
    // Transaction confirmed, cannot Rollback
}
catch
{
    _unitOfWork.Rollback();  // Undoes even flushed changes
    throw;
}
```

### FlushWhenNotActiveTransaction Pattern

Used in repositories to handle both transactional and non-transactional contexts:

```csharp
protected internal void FlushWhenNotActiveTransaction()
{
    var currentTransaction = this._session.GetCurrentTransaction();
    if (currentTransaction == null || !currentTransaction.IsActive)
        this._session.Flush();  // No transaction -> flush immediately
    // With transaction -> Commit() will flush
}
```

---

## Deadlock Prevention

Two transactions waiting for resources locked by each other:

```csharp
// DEADLOCK: Transaction A locks Users then waits for Roles
//           Transaction B locks Roles then waits for Users

// SOLUTION 1: Consistent resource ordering
var user = await _unitOfWork.Users.GetAsync(userId);  // Always Users first
var role = await _unitOfWork.Roles.GetAsync(roleId);  // Then Roles

// SOLUTION 2: Lower isolation level (ReadCommitted instead of Serializable)

// SOLUTION 3: Timeouts to detect deadlocks
```

---

## Common Problems

### Long-Running Transactions

```csharp
// WRONG: slow I/O inside transaction
_unitOfWork.BeginTransaction();
var user = await _unitOfWork.Users.CreateAsync(email);
await SendWelcomeEmail(email);  // 5 seconds
await GenerateReport();          // 10 seconds
_unitOfWork.Commit();

// CORRECT: slow I/O outside transaction
_unitOfWork.BeginTransaction();
try
{
    user = await _unitOfWork.Users.CreateAsync(email);
    _unitOfWork.Commit();
}
catch { _unitOfWork.Rollback(); throw; }

await SendWelcomeEmail(email);
await GenerateReport();
```

### Missing Rollback in Catch

```csharp
// WRONG: transaction left open on error
_unitOfWork.BeginTransaction();
try { /* ... */ _unitOfWork.Commit(); }
catch { throw; }  // No Rollback -> locks held

// CORRECT
catch { _unitOfWork.Rollback(); throw; }
```

---

## Anti-Patterns

**Silent Rollback** -- rollback but report success:

```csharp
catch { _unitOfWork.Rollback(); return Result.Ok(); }  // WRONG: hides error
```

**Partial Commit** -- committing mid-operation:

```csharp
_unitOfWork.BeginTransaction();
await _unitOfWork.Users.CreateAsync(email);
_unitOfWork.Commit();  // Premature
await _unitOfWork.Roles.AssignToUserAsync(userId);  // Outside transaction
```

**Nested Transactions** -- starting a second transaction inside the first:

```csharp
_unitOfWork.BeginTransaction();
_unitOfWork.BeginTransaction();  // ERROR
```

**Transactions on Read-Only Queries** -- unnecessary overhead:

```csharp
_unitOfWork.BeginTransaction();  // Unnecessary for reads
var users = await _unitOfWork.Users.GetAsync();
_unitOfWork.Commit();
```

---

## Checklist

Before implementing:
- Does the operation modify data? (No -> skip transaction)
- Are there multiple operations that must be atomic?
- What isolation level is appropriate?
- Are there slow operations that should be outside the transaction?

During implementation:
- `BeginTransaction()` at use case start
- Try-catch wraps all operations
- `Commit()` in try, `Rollback()` in every catch
- Specific exceptions before generic
- `Result<T>` for return values

After implementation:
- Rollback tests pass
- Transaction logging in place
- No deadlocks under concurrency
- Transactions complete in < 2 seconds
