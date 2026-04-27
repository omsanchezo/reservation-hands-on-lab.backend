# Async/Await Patterns

## When to Use Async/Await

Use async/await for **I/O-bound** operations (database, HTTP, file system). Do NOT use for CPU-bound operations.

```csharp
// I/O-bound: database access
public async Task<User?> Handle(Guid userId, CancellationToken ct)
{
    return await _userRepository.GetByIdAsync(userId, ct);
}

// I/O-bound: HTTP call
public async Task<string> GetDataAsync(string url, CancellationToken ct)
{
    var response = await _httpClient.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
}
```

```csharp
// BAD: async/await for CPU-bound
public async Task<long> CalculateFactorialAsync(int n)
{
    await Task.Delay(0); // BAD
    long result = 1;
    for (int i = 2; i <= n; i++) result *= i;
    return result;
}

// GOOD: synchronous for CPU-bound
public long CalculateFactorial(int n)
{
    long result = 1;
    for (int i = 2; i <= n; i++) result *= i;
    return result;
}
```

| Operation | Async? | Reason |
|-----------|--------|--------|
| Database calls | Yes | I/O-bound |
| HTTP requests | Yes | I/O-bound |
| File read/write | Yes | I/O-bound |
| Math calculations | No | CPU-bound |
| String manipulation | No | CPU-bound |

## Async All the Way

Once you introduce async/await, use it through the **entire call chain**. Never block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.

```csharp
// BAD: mixing sync and async
public async Task<User> Handle(CreateUserCommand command, CancellationToken ct)
{
    var existingUser = _userRepository.GetByEmailAsync(command.Email, ct).Result; // DEADLOCK RISK
    _userRepository.SaveOrUpdateAsync(user, ct).Wait(); // DEADLOCK RISK
    _unitOfWork.CommitAsync(ct).GetAwaiter().GetResult(); // DEADLOCK RISK
    return user;
}

// GOOD: async all the way
public async Task<Result<User>> Handle(CreateUserCommand command, CancellationToken ct)
{
    var existingUser = await _userRepository.GetByEmailAsync(command.Email, ct);
    await _userRepository.SaveOrUpdateAsync(user, ct);
    await _unitOfWork.CommitAsync(ct);
    return Result.Ok(user);
}
```

## CancellationToken

Always accept `CancellationToken ct` as the last parameter and pass it to every async call.

```csharp
// Domain Layer
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IList<Order>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task SaveAsync(Order order, CancellationToken ct);
}

// Application Layer - pass ct through the chain
public async Task<IList<Order>> Handle(Guid userId, CancellationToken ct)
{
    return await _orderRepository.GetByUserIdAsync(userId, ct);
}
```

Check cancellation in loops:

```csharp
public async Task ProcessOrdersAsync(IList<Guid> orderIds, CancellationToken ct)
{
    foreach (var orderId in orderIds)
    {
        ct.ThrowIfCancellationRequested();
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order != null)
            await ProcessSingleOrderAsync(order, ct);
    }
}
```

```csharp
// BAD: not accepting or not using CancellationToken
public async Task ProcessDataAsync() { await Task.Delay(1000); }
public async Task ProcessDataAsync2(CancellationToken ct) { await Task.Delay(1000); } // ct not passed
public async Task ProcessDataAsync3(CancellationToken ct) { await SomeMethodAsync(CancellationToken.None); } // ignores ct
```

## Exception Handling

Exceptions in async methods are captured normally. Validate synchronously first, then do async work.

```csharp
public Task SendEmailAsync(string to, string subject, string body, CancellationToken ct)
{
    // Synchronous validations (thrown immediately)
    if (string.IsNullOrWhiteSpace(to))
        throw new ArgumentException("Recipient email cannot be empty", nameof(to));

    // Delegate to async private method
    return SendEmailCoreAsync(to, subject, body, ct);
}

private async Task SendEmailCoreAsync(string to, string subject, string body, CancellationToken ct)
{
    var response = await _httpClient.PostAsync("/api/send", JsonContent.Create(new { to, subject, body }), ct);
    response.EnsureSuccessStatusCode();
}
```

## Avoid Async Void

```csharp
// BAD: async void crashes the app on unhandled exceptions
public async void ProcessOrderAsync(Guid orderId) { ... }

// GOOD: async Task allows callers to catch exceptions
public async Task ProcessOrderAsync(Guid orderId, CancellationToken ct) { ... }
```

The **only** valid exception: UI event handlers (WPF/WinForms), and always wrap in try-catch.

## Anti-Patterns

### Async Over Sync

```csharp
// BAD: async method with no await
public async Task<int> GetCountAsync() { return 42; }

// GOOD: synchronous methods should be synchronous
public int GetCount() { return 42; }
```

### Fire and Forget Without Handling

```csharp
// BAD: exceptions are lost
_ = DoWorkAsync();

// GOOD: use BackgroundService with error handling
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try { await DoWorkAsync(stoppingToken); }
        catch (Exception ex) { _logger.LogError(ex, "Error in background work"); }
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}
```

### Catch and Re-wrap Losing Stack Trace

```csharp
// BAD: throw ex loses stack trace
catch (Exception ex) { throw ex; }

// GOOD: throw preserves stack trace
catch (InvalidOperationException) { throw; }
```

## Checklist: New Async Method

- [ ] Returns `Task` or `Task<T>` (NOT `async void` unless event handler)
- [ ] Accepts `CancellationToken ct` as last parameter
- [ ] Method name ends with `Async`
- [ ] Passes `CancellationToken` to all async calls
- [ ] All I/O uses `await` (no `.Result` or `.Wait()`)
- [ ] Parameter validations are synchronous at the start
- [ ] Exceptions are handled with try-catch
- [ ] No synchronous blocking mixed with async

## Checklist: Code Review

- [ ] No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- [ ] No `async void` outside event handlers
- [ ] All async methods end with `Async`
- [ ] `CancellationToken` passed correctly throughout
- [ ] No unnecessary `Task.Run` for simple operations
- [ ] No async-over-sync (async methods without await)
- [ ] `Task.WhenAll` used for parallelism when appropriate
- [ ] No potential deadlocks from mixing sync/async
