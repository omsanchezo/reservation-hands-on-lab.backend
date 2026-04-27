# Error Handling Reference

Complete reference for FluentResults error handling patterns in the Application Layer: Result types, exception-to-Result conversion, custom error classes, and ValidationError deserialization.

---

## Result<T> vs Result

### Result<T> -- Operations that return a value

```csharp
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
{
    // Success with value
    return Result.Ok(user);

    // Failure with typed error
    return Result.Fail<User>(new Error(ex.Message).CausedBy(ex));
}
```

### Result -- Operations without return value

```csharp
public async Task<Result> ExecuteAsync(Command command, CancellationToken ct)
{
    // Success without value
    return Result.Ok();

    // Failure
    return Result.Fail(new Error(ex.Message).CausedBy(ex));
}
```

### Result API

```csharp
// Success with value
Result<User> success = Result.Ok(user);

// Success without value
Result success = Result.Ok();

// Failure with simple message
Result<User> fail = Result.Fail<User>("User not found");

// Failure with Error object
Result<User> fail = Result.Fail<User>(
    new Error("Validation failed")
        .WithMetadata("Field", "Email")
);
```

---

## Exception-to-Result Conversion

### Complete Catch Block Template

```csharp
try
{
    _uoW.BeginTransaction();
    // Domain operation
    _uoW.Commit();
    return Result.Ok(entity);
}
catch (InvalidDomainException idex)
{
    _uoW.Rollback();
    var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
    var firstError = errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid data";
    return Result.Fail(
        new Error(firstError)
            .CausedBy(idex)
            .WithMetadata("ValidationErrors", idex)
    );
}
catch (DuplicatedDomainException ddex)
{
    _uoW.Rollback();
    return Result.Fail(new Error(ddex.Message).CausedBy(ddex));
}
catch (ResourceNotFoundException rnfex)
{
    _uoW.Rollback();
    return Result.Fail(new Error(rnfex.Message).CausedBy(rnfex));
}
catch (HttpRequestException httpEx)
{
    _uoW.Rollback();
    return Result.Fail(
        new Error($"External service error: {httpEx.Message}").CausedBy(httpEx)
    );
}
catch (Exception ex)
{
    _uoW.Rollback();
    _logger.LogError(ex, "Unexpected error in {UseCase}", nameof(Handler));
    return Result.Fail(new Error("An unexpected error occurred").CausedBy(ex));
}
```

### Exception Types and When They Occur

| Exception | When | Handler Pattern |
|-----------|------|-----------------|
| `InvalidDomainException` | Entity validation fails (FluentValidation) | Create, Update |
| `DuplicatedDomainException` | Uniqueness constraint violated | Create, Update |
| `ResourceNotFoundException` | Entity not found for update/delete | Update, Delete |
| `HttpRequestException` | External service call fails | Create (with external service) |
| `Exception` | Any unexpected error | All patterns (always last) |

### Catch Order Rule

Always from most specific to most general. The generic `Exception` catch must be **last**:

```csharp
// CORRECT: Specific first, generic last
catch (InvalidDomainException idex) { }
catch (DuplicatedDomainException ddex) { }
catch (ResourceNotFoundException rnfex) { }
catch (Exception ex) { }

// WRONG: Generic first catches everything
catch (Exception ex) { }                    // Catches everything
catch (InvalidDomainException idex) { }     // Never reached
```

---

## Custom Error Classes

### Factory Pattern

```csharp
namespace {project}.domain.errors;

/// <summary>
/// Factory class for creating user-related errors.
/// </summary>
public static class UserErrors
{
    public static UserNotFoundError UserNotFound(string userName)
        => new UserNotFoundError(userName);

    public static UserNotFoundError NotFound(Guid id)
        => new UserNotFoundError(id.ToString());
}

/// <summary>
/// Custom error for when a user is not found.
/// </summary>
public class UserNotFoundError : Error
{
    public UserNotFoundError(string identifier)
        : base($"User '{identifier}' not found.")
    {
        Metadata.Add("EntityType", nameof(User));
        Metadata.Add("Identifier", identifier);
    }
}
```

### Template for New Entity Errors

```csharp
namespace {project}.domain.errors;

public static class {Entity}Errors
{
    public static {Entity}NotFoundError NotFound(Guid id)
        => new {Entity}NotFoundError(id);
}

public class {Entity}NotFoundError : Error
{
    public {Entity}NotFoundError(Guid id)
        : base($"{nameof({Entity})} with ID '{id}' not found.")
    {
        Metadata.Add("EntityId", id);
        Metadata.Add("EntityType", nameof({Entity}));
    }
}
```

### Usage

```csharp
// In Get handler
return user == null
    ? Result.Fail(UserErrors.UserNotFound(request.UserName))
    : Result.Ok(user);

// In Update handler -- when ResourceNotFoundException is not thrown by the repository
var entity = await _uoW.Entities.GetByIdAsync(command.Id, ct);
if (entity == null)
    return Result.Fail({Entity}Errors.NotFound(command.Id));
```

---

## ValidationError Model

```csharp
namespace {project}.application.common;

/// <summary>
/// Helper class for validation error structure.
/// </summary>
public class ValidationError
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
}
```

### Deserialization Pattern

The `InvalidDomainException.Message` contains a JSON array:

```json
[
  {
    "ErrorMessage": "The [Email] cannot be null or empty",
    "ErrorCode": "Email",
    "PropertyName": "Email"
  }
]
```

Always use the null-safe pattern:

```csharp
var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid data";
```

---

## CausedBy() and WithMetadata()

### CausedBy() -- Preserve Exception Context

```csharp
// Preserves the original exception and stack trace for debugging
return Result.Fail(
    new Error("Failed to create user").CausedBy(idex)
);
```

### WithMetadata() -- Add Debugging Context

```csharp
return Result.Fail(
    new Error(firstErrorMessage)
        .CausedBy(idex)
        .WithMetadata("ValidationErrors", idex)
        .WithMetadata("UserId", command.Id)
        .WithMetadata("Timestamp", DateTime.UtcNow)
);
```

### Combined Pattern

```csharp
// Real-world example from CreatePrototypeUseCase
catch (InvalidDomainException idex)
{
    _uoW.Rollback();
    var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
    var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage
        ?? "Invalid prototype data";
    return Result.Fail(
        new Error(firstErrorMessage)
            .CausedBy(idex)                              // Exception chain
            .WithMetadata("ValidationErrors", idex)       // Full error details
    );
}
```

---

## Best Practices

1. **Result for control flow, exceptions for unexpected errors**
   ```csharp
   // CORRECT: Result for business errors
   if (user == null)
       return Result.Fail(UserErrors.UserNotFound(id));

   // WRONG: Exception for control flow
   if (user == null)
       throw new UserNotFoundException(id);
   ```

2. **Always Rollback in catch blocks**
   ```csharp
   catch (InvalidDomainException ex)
   {
       _uoW.Rollback();  // Critical -- prevents open transactions
       return Result.Fail(new Error(ex.Message).CausedBy(ex));
   }
   ```

3. **Always use CausedBy() to preserve stack trace**
   ```csharp
   // CORRECT
   return Result.Fail(new Error("Operation failed").CausedBy(ex));

   // WRONG -- loses exception context
   return Result.Fail(new Error("Operation failed"));
   ```

4. **Use custom errors for common cases**
   ```csharp
   // CORRECT: Typed, reusable
   return Result.Fail(UserErrors.UserNotFound(userName));

   // WRONG: Magic string repeated everywhere
   return Result.Fail($"User '{userName}' not found.");
   ```
