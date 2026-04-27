# Result Pattern

## Result<T> with FluentResults

Use `Result<T>` for **expected** errors (validations, business rules, not-found). Use exceptions for **unexpected** errors (infrastructure failures).

### Basic Usage

```csharp
public async Task<Result<User>> Handle(string email, CancellationToken ct)
{
    var user = await _userRepository.GetByEmailAsync(email, ct);
    if (user == null)
        return Result.Fail<User>($"User with email '{email}' not found");
    return Result.Ok(user);
}
```

### Result with Multiple Errors

```csharp
public async Task<Result<User>> Handle(CreateUserCommand command, CancellationToken ct)
{
    var validationResult = await _validator.ValidateAsync(command, ct);
    if (!validationResult.IsValid)
    {
        var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
        return Result.Fail<User>(errors);
    }

    var existingUser = await _userRepository.GetByEmailAsync(command.Email, ct);
    if (existingUser != null)
        return Result.Fail<User>("Email already registered");

    var user = new User { Email = command.Email, FirstName = command.FirstName };
    await _userRepository.SaveOrUpdateAsync(user, ct);
    return Result.Ok(user);
}
```

## Custom Error Types

```csharp
public class NotFoundError : Error
{
    public string EntityType { get; }
    public object EntityId { get; }

    public NotFoundError(string entityType, object entityId)
        : base($"{entityType} with ID '{entityId}' not found")
    {
        EntityType = entityType;
        EntityId = entityId;
        Metadata.Add("ErrorCode", "NOT_FOUND");
    }
}

public class ValidationError : Error
{
    public Dictionary<string, string[]> ValidationErrors { get; }

    public ValidationError(Dictionary<string, string[]> errors)
        : base("Validation error")
    {
        ValidationErrors = errors;
        Metadata.Add("ErrorCode", "VALIDATION_ERROR");
    }
}

public class BusinessRuleError : Error
{
    public string RuleCode { get; }

    public BusinessRuleError(string ruleCode, string message)
        : base(message)
    {
        RuleCode = ruleCode;
        Metadata.Add("ErrorCode", "BUSINESS_RULE_VIOLATION");
    }
}
```

## When Exceptions vs Results

| Scenario | Use | Reason |
|----------|-----|--------|
| Input validation | `Result<T>` | Expected, part of normal flow |
| Business rule violated | `Result<T>` | Expected, part of the domain |
| Resource not found | `Result<T>` | Expected case, not exceptional |
| Duplicate email | `Result<T>` | Uniqueness validation, expected |
| Insufficient stock | `Result<T>` | Business rule, expected |
| DB connection failure | `Exception` | Unexpected infrastructure error |
| Network timeout | `Exception` | Unexpected infrastructure error |
| ArgumentNullException | `Exception` | Programming error |

## Error Propagation Between Layers

**Rules:**
1. **Domain -> Application**: Application catches domain exceptions and converts to Results
2. **Application -> WebApi**: WebApi maps Results to HTTP responses
3. **Infrastructure exceptions**: Propagate to the Global Exception Handler

```csharp
// Domain: throws exceptions for critical invariants
public virtual void Cancel()
{
    if (Status != OrderStatus.Pending)
        throw new InvalidStateTransitionException(
            Status.ToString(), OrderStatus.Cancelled.ToString(),
            $"Cannot cancel an order in {Status} state.");
    Status = OrderStatus.Cancelled;
}

// Application: converts domain exceptions to Results
public async Task<Result> Handle(Guid orderId, CancellationToken ct)
{
    var order = await _orderRepository.GetByIdAsync(orderId, ct);
    if (order == null)
        return Result.Fail(new NotFoundError("Order", orderId));

    try
    {
        order.Cancel();
    }
    catch (InvalidStateTransitionException ex)
    {
        return Result.Fail(new BusinessRuleError(ex.ErrorCode, ex.Message));
    }

    await _orderRepository.SaveOrUpdateAsync(order, ct);
    return Result.Ok();
}
```

## HTTP Mapping

```csharp
public static class ResultMapper
{
    public static (int StatusCode, string Message) MapToHttpResponse(IError error)
    {
        return error switch
        {
            NotFoundError => (404, error.Message),
            ValidationError => (400, error.Message),
            BusinessRuleError => (422, error.Message),
            _ => (400, error.Message)
        };
    }
}
```

```csharp
// WebApi endpoint handling Results
public override async Task HandleAsync(WithdrawRequest req, CancellationToken ct)
{
    var result = await _handler.Handle(req.AccountId, req.Amount, ct);
    if (result.IsFailed)
    {
        var error = result.Errors.First();
        if (error is NotFoundError) { await SendNotFoundAsync(ct); return; }
        if (error is ValidationError ve)
        {
            foreach (var kvp in ve.ValidationErrors)
                foreach (var msg in kvp.Value) AddError(msg, kvp.Key);
            await SendErrorsAsync(cancellation: ct); return;
        }
        if (error is BusinessRuleError be)
        {
            AddError(be.Message);
            await SendErrorsAsync(statusCode: 422, cancellation: ct); return;
        }
    }
    await SendOkAsync(new WithdrawResponse { NewBalance = result.Value }, ct);
}
```

## Anti-Patterns

### Catching and Hiding Exceptions

```csharp
// BAD: silently returns null
public async Task<User?> GetUserAsync(Guid id, CancellationToken ct)
{
    try { return await _repository.GetByIdAsync(id, ct); }
    catch (Exception) { return null; }
}

// GOOD: let infrastructure exceptions propagate
public async Task<Result<User>> GetUserAsync(Guid id, CancellationToken ct)
{
    var user = await _repository.GetByIdAsync(id, ct);
    if (user == null) return Result.Fail<User>(new NotFoundError("User", id));
    return Result.Ok(user);
}
```

### Using Exceptions for Flow Control

```csharp
// BAD: exception for expected case
try { var vipStatus = user.GetVipStatus(); return vipStatus.DiscountPercentage; }
catch (VipStatusNotFoundException) { return 0m; }

// GOOD: explicit return
var vipStatus = user.GetVipStatusOrDefault();
return vipStatus?.DiscountPercentage ?? 0m;
```

### Non-Informative Error Messages

```csharp
// BAD
return Result.Fail("Error processing");
return Result.Fail("Not found");

// GOOD
return Result.Fail(new NotFoundError("Product", productId));
return Result.Fail(new BusinessRuleError("INSUFFICIENT_STOCK",
    $"Insufficient stock for '{productName}'. Available: {available}, Requested: {requested}"));
```

## Checklist: Error Handling in Use Case

- [ ] Input validation returns `Result.Fail` with `ValidationError`
- [ ] Required entities checked; `NotFoundError` if missing
- [ ] Business rules validated; `BusinessRuleError` with descriptive code and message
- [ ] Domain exceptions caught in try-catch and converted to `Result.Fail`
- [ ] Infrastructure exceptions left to propagate (logged with `_logger.LogError` before re-throw)
- [ ] Returns `Result<T>` or `Result` (not void)
- [ ] Does not mix exceptions and Results for the same concern
- [ ] Uses `throw;` (not `throw ex;`) to preserve stack traces
