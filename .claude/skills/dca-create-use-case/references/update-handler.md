# Update Handler Reference

Complete reference for the Update pattern: modifying existing entities with transaction management, not-found handling, and validation errors.

---

## Template

```csharp
using FastEndpoints;
using FluentResults;
using {project}.domain.entities;
using {project}.domain.exceptions;
using {project}.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace {project}.application.usecases.{feature};

public class Update{Entity}UseCase
{
    public class Command : ICommand<Result<{Entity}>>
    {
        public Guid Id { get; set; }
        // Properties to update
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
            try
            {
                _uoW.BeginTransaction();

                _logger.LogInformation("Updating {entity}: {Id}",
                    nameof({Entity}), command.Id);

                var updated = await _uoW.{Entities}.UpdateAsync(
                    command.Id,
                    command.Property1,
                    command.Property2
                );

                _uoW.Commit();
                _logger.LogInformation("{Entity} updated: {Id}", nameof({Entity}), command.Id);
                return Result.Ok(updated);
            }
            catch (ResourceNotFoundException ex)
            {
                _uoW.Rollback();
                return Result.Fail<{Entity}>(new Error(ex.Message).CausedBy(ex));
            }
            catch (InvalidDomainException ex)
            {
                _uoW.Rollback();
                return Result.Fail<{Entity}>(new Error(ex.Message).CausedBy(ex));
            }
            catch (DuplicatedDomainException ex)
            {
                _uoW.Rollback();
                return Result.Fail<{Entity}>(new Error(ex.Message).CausedBy(ex));
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                _logger.LogError(ex, "Error updating {entity}: {Id}",
                    nameof({Entity}), command.Id);
                return Result.Fail<{Entity}>(
                    new Error("Internal server error: " + ex.Message).CausedBy(ex)
                );
            }
        }
    }
}
```

**Key characteristics:**
- Returns `Result<Entity>`
- Command always includes `Guid Id` to identify the entity to update
- Catches `ResourceNotFoundException` (entity doesn't exist)
- Catches `InvalidDomainException` (validation fails after update)
- Catches `DuplicatedDomainException` (uniqueness constraint violated)
- Uses `Result.Fail<Entity>()` (generic version) for typed Result failures

---

## Real Example: UpdateTechnicalStandardUseCase

```csharp
using FastEndpoints;
using FluentResults;
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.exceptions;
using hashira.stone.backend.domain.interfaces.repositories;

namespace hashira.stone.backend.application.usecases.technicalstandards;

public class UpdateTechnicalStandardUseCase
{
    public class Command : ICommand<Result<TechnicalStandard>>
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uow) : ICommandHandler<Command, Result<TechnicalStandard>>
    {
        private readonly IUnitOfWork _uow = uow;

        public async Task<Result<TechnicalStandard>> ExecuteAsync(
            Command request, CancellationToken ct)
        {
            try
            {
                _uow.BeginTransaction();

                var updated = await _uow.TechnicalStandards.UpdateAsync(
                    request.Id,
                    request.Code,
                    request.Name,
                    request.Edition,
                    request.Status,
                    request.Type
                );

                _uow.Commit();
                return Result.Ok(updated);
            }
            catch (ResourceNotFoundException ex)
            {
                _uow.Rollback();
                return Result.Fail<TechnicalStandard>(new Error(ex.Message).CausedBy(ex));
            }
            catch (InvalidDomainException ex)
            {
                _uow.Rollback();
                return Result.Fail<TechnicalStandard>(new Error(ex.Message).CausedBy(ex));
            }
            catch (DuplicatedDomainException ex)
            {
                _uow.Rollback();
                return Result.Fail<TechnicalStandard>(new Error(ex.Message).CausedBy(ex));
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                return Result.Fail<TechnicalStandard>(
                    new Error("Internal server error: " + ex.Message).CausedBy(ex)
                );
            }
        }
    }
}
```

---

## Variant: Partial Update

For updating only specific properties (lock/unlock, status change, etc.):

```csharp
public class UpdateUserLockUseCase
{
    public class Command : ICommand<Result<User>>
    {
        public Guid Id { get; set; }
        public bool Locked { get; set; }
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, Result<User>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
        {
            try
            {
                _uoW.BeginTransaction();

                // Get entity first
                var user = await _uoW.Users.GetByIdAsync(command.Id, ct);
                if (user == null)
                    return Result.Fail(UserErrors.UserNotFound(command.Id));

                // Update specific property
                user.Locked = command.Locked;

                // Save changes
                await _uoW.Users.UpdateAsync(user);

                _uoW.Commit();
                return Result.Ok(user);
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                _logger.LogError(ex, "Error updating user lock status");
                return Result.Fail<User>(new Error(ex.Message).CausedBy(ex));
            }
        }
    }
}
```

**Partial update flow:**
1. Get the entity by ID (check null)
2. Modify only the specific properties
3. Save the entity via `UpdateAsync`
4. Commit the transaction
