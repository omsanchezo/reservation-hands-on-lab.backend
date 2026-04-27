# Get Handler Reference

Complete reference for the Get pattern: retrieving a single entity by ID or unique field, without transaction management.

---

## Template

```csharp
using FastEndpoints;
using FluentResults;
using {project}.domain.entities;
using {project}.domain.errors;
using {project}.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace {project}.application.usecases.{feature};

public class Get{Entity}UseCase
{
    public class Command : ICommand<Result<{Entity}>>
    {
        public Guid Id { get; set; }
        // Or any other unique identifier:
        // public string Username { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, Result<{Entity}>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<Result<{Entity}>> ExecuteAsync(Command command, CancellationToken ct)
        {
            try
            {
                var entity = await _uoW.{Entities}.GetByIdAsync(command.Id, ct);

                if (entity == null)
                {
                    return Result.Fail({Entity}Errors.NotFound(command.Id));
                    // Or use a direct message if no custom error class exists:
                    // return Result.Fail($"{nameof({Entity})} not found");
                }

                return Result.Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {entity}: {id}",
                    nameof({Entity}), command.Id);
                return Result.Fail($"Error retrieving {nameof({Entity}).ToLower()}");
            }
        }
    }
}
```

**Key characteristics:**
- Returns `Result<Entity>`
- **NO** `BeginTransaction()` / `Commit()` / `Rollback()` -- read-only operation
- Returns `Result.Fail()` if entity is null (not found)
- Uses custom error class when available (e.g., `UserErrors.UserNotFound()`)
- Try-catch only for unexpected errors

---

## Real Example: GetUserUseCase

```csharp
using FastEndpoints;
using FluentResults;
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.errors;
using hashira.stone.backend.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace hashira.stone.backend.application.usecases.users;

public class GetUserUseCase
{
    public class Command : ICommand<Result<User>>
    {
        public string UserName { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, Result<User>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<Result<User>> ExecuteAsync(Command request, CancellationToken ct)
        {
            try
            {
                var user = await _uoW.Users.GetByEmailAsync(request.UserName);

                return user == null
                    ? Result.Fail(UserErrors.UserNotFound(request.UserName))
                    : Result.Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user: {UserName}", request.UserName);
                return Result.Fail("Error retrieving user");
            }
        }
    }
}
```

---

## Custom Error Class Usage

When a Get handler returns "not found", use a custom error class for consistent, typed errors:

```csharp
// In {project}.domain.errors/UserErrors.cs
public static class UserErrors
{
    public static UserNotFoundError UserNotFound(string userName)
        => new UserNotFoundError(userName);
}

public class UserNotFoundError : Error
{
    public UserNotFoundError(string userName)
        : base($"User '{userName}' not found.") { }
}
```

Usage in handler:

```csharp
return user == null
    ? Result.Fail(UserErrors.UserNotFound(request.UserName))
    : Result.Ok(user);
```

If no custom error class exists yet, use a direct message:

```csharp
return entity == null
    ? Result.Fail($"{nameof(Order)} with id {command.Id} not found")
    : Result.Ok(entity);
```
