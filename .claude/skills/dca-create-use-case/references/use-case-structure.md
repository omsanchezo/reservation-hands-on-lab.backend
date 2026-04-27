# Use Case Structure Reference

Complete reference for use case anatomy, naming conventions, lifecycle, and FastEndpoints interfaces.

---

## Full Template

```csharp
using FastEndpoints;
using FluentResults;
using {project}.domain.entities;
using {project}.application.common;
using {project}.domain.exceptions;
using {project}.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace {project}.application.usecases.{feature};

/// <summary>
/// Use case for {operation description}.
/// </summary>
public abstract class {Operation}{Entity}UseCase
{
    /// <summary>
    /// Command to {operation description}.
    /// </summary>
    public class Command : ICommand<{ResultType}>
    {
        /// <summary>
        /// Gets or sets the {property description}.
        /// </summary>
        public {Type} {Property} { get; set; } = {default};
    }

    /// <summary>
    /// Handler for executing the {operation} command.
    /// </summary>
    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, {ResultType}>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        /// <summary>
        /// Executes the command to {operation description}.
        /// </summary>
        public async Task<{ResultType}> ExecuteAsync(Command command, CancellationToken ct)
        {
            // Implementation based on operation type
        }
    }
}
```

---

## Naming Conventions

### File Naming

```
{Operation}{Entity}UseCase.cs
```

Examples:
- `CreateUserUseCase.cs`
- `GetUserUseCase.cs`
- `UpdateUserUseCase.cs`
- `DeleteUserUseCase.cs`
- `GetManyAndCountUsersUseCase.cs`
- `AddUsersToRoleUseCase.cs`

### Namespace

```csharp
namespace {project}.application.usecases.{feature};
```

Examples:
- `hashira.stone.backend.application.usecases.users`
- `hashira.stone.backend.application.usecases.prototypes`
- `hashira.stone.backend.application.usecases.technicalstandards`

### Command and Handler

**Always** use the names `Command` and `Handler` -- never prefix with the entity or operation name:

```csharp
// CORRECT
public class Command : ICommand<Result<User>> { }
public class Handler : ICommandHandler<Command, Result<User>> { }

// WRONG
public class CreateUserCommand : ICommand<Result<User>> { }
public class CreateUserHandler : ICommandHandler<CreateUserCommand, Result<User>> { }
```

---

## ICommand<TResult> Interface

Marker interface from FastEndpoints that defines the result type:

```csharp
// Entity result (Create, Get, Update)
public class Command : ICommand<Result<User>> { }

// Void result (Delete)
public class Command : ICommand<Result> { }

// Paginated list result (GetManyAndCount)
public class Command : ICommand<GetManyAndCountResult<User>> { }
```

**Properties:** Command classes contain input data with XML comments and default values:

```csharp
public class Command : ICommand<Result<TechnicalStandard>>
{
    /// <summary>
    /// Gets or sets the ID of the technical standard to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
```

---

## ICommandHandler<TCommand, TResult> Interface

```csharp
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct);
}
```

Features:
- **Generic**: binds a specific command to its result type
- **ExecuteAsync**: single method for executing the command
- **CancellationToken**: support for async cancellation
- **Dependencies**: injected via primary constructor

---

## Use Case Lifecycle

### 1. Invocation from Endpoint

```csharp
// WebApi Endpoint
public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
{
    var command = new CreateUserUseCase.Command
    {
        Email = req.Email,
        Name = req.Name
    };

    var result = await command.ExecuteAsync(ct);
    // Map result to HTTP response
}
```

### 2. Handler Execution Flow

```
1. FastEndpoints resolves ICommandHandler<Command, Result>
2. Injects dependencies (IUnitOfWork, ILogger, etc.)
3. Calls ExecuteAsync(command, ct)
4. Handler orchestrates the operation
5. Returns Result<T>
```

### 3. Internal Flow (Write Operations)

```
1. BeginTransaction()
2. Logging (operation start)
3. Domain/repository operations
4. Commit() on success / Rollback() on error
5. Logging (operation end)
6. Return Result.Ok() or Result.Fail()
```

---

## Abstract vs Class

- **`abstract`**: when the use case contains static helper methods

```csharp
public abstract class CreateUserUseCase
{
    public class Command : ICommand<Result<User>> { ... }
    public class Handler : ICommandHandler<Command, Result<User>> { ... }

    private static string GenerateRandomPassword() { ... }
}
```

- **`class`** (no modifier): when it only contains Command + Handler

```csharp
public class GetUserUseCase
{
    public class Command : ICommand<Result<User>> { ... }
    public class Handler : ICommandHandler<Command, Result<User>> { ... }
}
```

---

## Anti-Patterns

```csharp
// NEVER: Return entity directly without Result
public class Command : ICommand<User> { }  // Missing Result<> wrapper

// NEVER: Separate Command and Handler classes
public class CreateUserCommand : ICommand<Result<User>> { }
public class CreateUserHandler : ICommandHandler<CreateUserCommand, Result<User>> { }

// NEVER: Multiple operations in one use case
public abstract class UserCrudUseCase { }  // Split into Create, Get, Update, Delete

// NEVER: Business logic in the handler
if (user.Email.Length < 5)
    return Result.Fail("Email too short");  // This belongs in Domain (UserValidator)

// NEVER: Throw exceptions instead of returning Result
if (user == null)
    throw new NotFoundException("User not found");  // Use Result.Fail() instead

// NEVER: Old-style constructor (C# 12 and earlier)
public class Handler : ICommandHandler<Command, Result<User>>
{
    private readonly IUnitOfWork _uoW;
    public Handler(IUnitOfWork uoW) { _uoW = uoW; }  // Use primary constructor
}
```
