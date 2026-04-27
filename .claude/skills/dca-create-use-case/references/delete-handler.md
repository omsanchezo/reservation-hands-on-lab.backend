# Delete Handler Reference

Complete reference for the Delete pattern: removing entities with transaction management, not-found handling, and soft delete variant.

---

## Template

```csharp
using FastEndpoints;
using FluentResults;
using {project}.domain.exceptions;
using {project}.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace {project}.application.usecases.{feature};

public class Delete{Entity}UseCase
{
    public class Command : ICommand<Result>
    {
        public Guid Id { get; set; }
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, Result>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<Result> ExecuteAsync(Command command, CancellationToken ct)
        {
            try
            {
                _uoW.BeginTransaction();

                _logger.LogInformation("Deleting {entity}: {Id}",
                    nameof({Entity}), command.Id);

                await _uoW.{Entities}.DeleteAsync(command.Id, ct);

                _uoW.Commit();
                _logger.LogInformation("{Entity} deleted: {Id}",
                    nameof({Entity}), command.Id);
                return Result.Ok();
            }
            catch (ResourceNotFoundException ex)
            {
                _uoW.Rollback();
                return Result.Fail(new Error(ex.Message).CausedBy(ex));
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                _logger.LogError(ex, "Error deleting {entity}: {Id}",
                    nameof({Entity}), command.Id);
                return Result.Fail(new Error(ex.Message).CausedBy(ex));
            }
        }
    }
}
```

**Key characteristics:**
- Returns `Result` (void -- no entity returned)
- Command only needs `Guid Id`
- Catches `ResourceNotFoundException` (entity doesn't exist)
- Simpler catch structure than Create/Update (no validation or duplication errors)

---

## Result vs Result<Entity>

| Return Type | When to Use |
|-------------|------------|
| `Result` | Hard delete -- entity is removed, nothing to return |
| `Result<Entity>` | Soft delete -- entity is marked as deleted, return updated entity |

---

## Variant: Soft Delete

When entities should be marked as deleted rather than physically removed:

```csharp
public class SoftDelete{Entity}UseCase
{
    public class Command : ICommand<Result<{Entity}>>
    {
        public Guid Id { get; set; }
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

                var entity = await _uoW.{Entities}.GetByIdAsync(command.Id, ct);
                if (entity == null)
                    return Result.Fail({Entity}Errors.NotFound(command.Id));

                // Soft delete - mark as deleted
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;

                await _uoW.{Entities}.UpdateAsync(entity);

                _uoW.Commit();
                return Result.Ok(entity);
            }
            catch (Exception ex)
            {
                _uoW.Rollback();
                _logger.LogError(ex, "Error soft deleting {entity}", command.Id);
                return Result.Fail<{Entity}>(new Error(ex.Message).CausedBy(ex));
            }
        }
    }
}
```

**Soft delete flow:**
1. Get entity by ID (check null)
2. Set `IsDeleted = true` and `DeletedAt = DateTime.UtcNow`
3. Call `UpdateAsync` (not `DeleteAsync`)
4. Commit transaction
5. Return the updated entity (with `Result<Entity>`, not `Result`)
