# Create Handler Reference

Complete reference for the Create pattern: creating new entities with transaction management, validation error handling, and external service integration.

---

## Template

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

public abstract class Create{Entity}UseCase
{
    public class Command : ICommand<Result<{Entity}>>
    {
        public string Property1 { get; set; } = string.Empty;
        public string Property2 { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, Result<{Entity}>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<Result<{Entity}>> ExecuteAsync(Command command, CancellationToken ct)
        {
            _uoW.BeginTransaction();
            try
            {
                _logger.LogInformation("Creating {entity}: {identifier}",
                    nameof({Entity}), command.Property1);

                var entity = await _uoW.{Entities}.CreateAsync(
                    command.Property1,
                    command.Property2
                );

                _uoW.Commit();
                _logger.LogInformation("{Entity} created: {Id}", nameof({Entity}), entity.Id);
                return Result.Ok(entity);
            }
            catch (InvalidDomainException idex)
            {
                _uoW.Rollback();
                var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
                var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage
                    ?? $"Invalid {nameof({Entity}).ToLower()} data";
                return Result.Fail(
                    new Error(firstErrorMessage)
                        .CausedBy(idex)
                        .WithMetadata("ValidationErrors", idex)
                );
            }
            catch (DuplicatedDomainException ddex)
            {
                _uoW.Rollback();
                _logger.LogWarning("Duplicate {entity}: {message}",
                    nameof({Entity}), ddex.Message);
                return Result.Fail(new Error(ddex.Message).CausedBy(ddex));
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                _logger.LogError(ex, "Error creating {entity}", nameof({Entity}));
                return Result.Fail(new Error(ex.Message).CausedBy(ex));
            }
        }
    }
}
```

---

## Real Example: CreatePrototypeUseCase

```csharp
using FastEndpoints;
using FluentResults;
using hashira.stone.backend.application.common;
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.exceptions;
using hashira.stone.backend.domain.interfaces.repositories;
using System.Text.Json;

namespace hashira.stone.backend.application.usecases.prototypes;

public abstract class CreatePrototypeUseCase
{
    public class Command : ICommand<Result<Prototype>>
    {
        public string Number { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW) : ICommandHandler<Command, Result<Prototype>>
    {
        private readonly IUnitOfWork _uoW = uoW;

        public async Task<Result<Prototype>> ExecuteAsync(Command command, CancellationToken ct)
        {
            _uoW.BeginTransaction();
            try
            {
                var prototype = await _uoW.Prototypes.CreateAsync(
                    command.Number,
                    command.IssueDate,
                    command.ExpirationDate,
                    command.Status
                );
                _uoW.Commit();
                return Result.Ok(prototype);
            }
            catch (InvalidDomainException idex)
            {
                _uoW.Rollback();
                var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
                var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage
                    ?? "Invalid prototype data";
                return Result.Fail(
                    new Error(firstErrorMessage)
                        .CausedBy(idex)
                        .WithMetadata("ValidationErrors", idex)
                );
            }
            catch (DuplicatedDomainException ddex)
            {
                _uoW.Rollback();
                return Result.Fail(new Error(ddex.Message).CausedBy(ddex));
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                return Result.Fail(new Error(ex.Message).CausedBy(ex));
            }
        }
    }
}
```

---

## Variant: Create with External Service

When creating in an external service (Auth0, payment gateway, etc.) before the domain entity:

```csharp
public class Handler(IUnitOfWork uoW, IIdentityService identityService, ILogger<Handler> logger)
    : ICommandHandler<Command, Result<User>>
{
    private readonly IUnitOfWork _uoW = uoW;
    private readonly IIdentityService _identityService = identityService;
    private readonly ILogger<Handler> _logger = logger;

    public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
    {
        _uoW.BeginTransaction();
        try
        {
            _logger.LogInformation("Creating user: {Email}", command.Email);

            // 1. Create in external service first
            var password = GenerateRandomPassword();
            var auth0User = _identityService.Create(command.Email, command.Name, password);

            // 2. Create in domain
            var user = await _uoW.Users.CreateAsync(command.Email, command.Name);

            _uoW.Commit();
            _logger.LogInformation("User created: {Id}", user.Id);
            return Result.Ok(user);
        }
        catch (HttpRequestException httpEx)
        {
            _uoW.Rollback();
            return Result.Fail(
                new Error($"Error creating user on authentication service")
                    .CausedBy(httpEx)
            );
        }
        catch (InvalidDomainException idex)
        {
            _uoW.Rollback();
            var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
            var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage
                ?? "Invalid user data";
            return Result.Fail(
                new Error(firstErrorMessage)
                    .CausedBy(idex)
                    .WithMetadata("ValidationErrors", idex)
            );
        }
        catch (DuplicatedDomainException ddex)
        {
            _uoW.Rollback();
            return Result.Fail(new Error(ddex.Message).CausedBy(ddex));
        }
        catch (Exception ex)
        {
            _uoW.Rollback();
            _logger.LogError(ex, "Error creating user");
            return Result.Fail(new Error(ex.Message).CausedBy(ex));
        }
    }
}
```

**Key difference:** When using external services, add a specific catch for `HttpRequestException` (or the service's exception type) before the domain exceptions. This provides better error messages for external service failures.

---

## ValidationError Deserialization

The `InvalidDomainException.Message` contains a JSON array of validation errors from FluentValidation:

```json
[
  {
    "ErrorMessage": "The [Email] cannot be null or empty",
    "ErrorCode": "Email",
    "PropertyName": "Email"
  },
  {
    "ErrorMessage": "The [Name] cannot be null or empty",
    "ErrorCode": "Name",
    "PropertyName": "Name"
  }
]
```

Always use the null-safe deserialization pattern:

```csharp
var errors = JsonSerializer.Deserialize<List<ValidationError>>(idex.Message);
var firstErrorMessage = errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid data";
```

The `ValidationError` class lives in `{project}.application.common`:

```csharp
public class ValidationError
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
}
```
