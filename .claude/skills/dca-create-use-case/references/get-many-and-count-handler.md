# GetManyAndCount Handler Reference

Complete reference for the GetManyAndCount pattern: paginated lists with total count, query filtering, and sort configuration.

---

## Template

```csharp
using FastEndpoints;
using {project}.domain.entities;
using {project}.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace {project}.application.usecases.{feature};

public abstract class GetManyAndCount{Entities}UseCase
{
    public class Command : ICommand<GetManyAndCountResult<{Entity}>>
    {
        public string? Query { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, GetManyAndCountResult<{Entity}>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<GetManyAndCountResult<{Entity}>> ExecuteAsync(
            Command command,
            CancellationToken ct)
        {
            try
            {
                _uoW.BeginTransaction();

                _logger.LogInformation("Getting {entities} with query: {Query}",
                    nameof({Entity}), command.Query);

                var results = await _uoW.{Entities}.GetManyAndCountAsync(
                    command.Query,
                    nameof({Entity}.{DefaultSortField}),
                    ct
                );

                _logger.LogInformation("Retrieved {Count} {entities}",
                    results.Count, nameof({Entity}));

                _uoW.Commit();
                return results;
            }
            catch
            {
                _uoW.Rollback();
                throw;
            }
        }
    }
}
```

**Key characteristics:**
- Returns `GetManyAndCountResult<T>` (not wrapped in `Result<T>`)
- **Uses transaction** -- NHibernate requires it for complex queries with count
- Catch block does `Rollback()` then **rethrows** (`throw;`), not `Result.Fail()`
- Receives `Query` for search/filtering
- Specifies default sort field via `nameof(Entity.Property)`

---

## Real Example: GetManyAndCountUsersUseCase

```csharp
using FastEndpoints;
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;

namespace hashira.stone.backend.application.usecases.users;

public abstract class GetManyAndCountUsersUseCase
{
    public class Command : ICommand<GetManyAndCountResult<User>>
    {
        public string? Query { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, GetManyAndCountResult<User>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<GetManyAndCountResult<User>> ExecuteAsync(
            Command command,
            CancellationToken ct)
        {
            try
            {
                _uoW.BeginTransaction();
                _logger.LogInformation(
                    "Executing GetManyAndCountUsersUseCase with query: {Query}",
                    command.Query);

                var users = await _uoW.Users.GetManyAndCountAsync(
                    command.Query,
                    nameof(User.Email),
                    ct
                );

                _logger.LogInformation(
                    "End GetManyAndCountUsersUseCase with total users: {TotalUsers}",
                    users.Count);

                _uoW.Commit();
                return users;
            }
            catch
            {
                _uoW.Rollback();
                throw;
            }
        }
    }
}
```

---

## Variant: GetManyAndCount with DAO

For optimized read-only queries, use a DAO instead of the entity:

```csharp
public abstract class GetManyAndCountPrototypesUseCase
{
    public class Command : ICommand<GetManyAndCountResult<PrototypeDao>>
    {
        public string? Query { get; set; } = string.Empty;
    }

    public class Handler(IUnitOfWork uoW, ILogger<Handler> logger)
        : ICommandHandler<Command, GetManyAndCountResult<PrototypeDao>>
    {
        private readonly IUnitOfWork _uoW = uoW;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<GetManyAndCountResult<PrototypeDao>> ExecuteAsync(
            Command command,
            CancellationToken ct)
        {
            try
            {
                _uoW.BeginTransaction();

                var results = await _uoW.PrototypesDao.GetManyAndCountAsync(
                    command.Query,
                    nameof(PrototypeDao.Number),
                    ct
                );

                _uoW.Commit();
                return results;
            }
            catch
            {
                _uoW.Rollback();
                throw;
            }
        }
    }
}
```

**When to use DAO:** Use `EntityDao` instead of `Entity` when the list view needs flattened relationships or `SearchAll` for full-text search. See `create-domain` skill for DAO patterns.

---

## Rethrow Pattern

GetManyAndCount uses `throw;` instead of `Result.Fail()` because:

1. The return type is `GetManyAndCountResult<T>`, not `Result<T>`
2. Errors in list queries are unexpected (not business errors)
3. The WebApi layer handles the exception and maps to appropriate HTTP status

```csharp
// CORRECT: Rethrow for GetManyAndCount
catch
{
    _uoW.Rollback();
    throw;  // Let WebApi handle the exception
}

// WRONG: Result.Fail on GetManyAndCount
catch (Exception ex)
{
    _uoW.Rollback();
    return Result.Fail(...);  // GetManyAndCountResult is not Result<T>
}
```
