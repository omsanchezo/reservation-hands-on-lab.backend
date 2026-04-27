---
name: create-use-case
description: >-
  Guides creation of Application Layer use cases in APSYS .NET backend projects.
  Covers the Command/Handler pattern with FastEndpoints, FluentResults error handling,
  transaction management, and the five CRUD handler patterns (Create, Get, GetManyAndCount,
  Update, Delete). Use when user asks to "create a use case", "add a handler",
  "implement CRUD operations", "handle errors with Result", "add transaction management",
  or "write application layer tests" in Clean Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with FastEndpoints,
  FluentResults, and NHibernate. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 2.0.0
---

# Application Layer Skill

Guide for creating and maintaining Application Layer use cases in APSYS .NET backend projects:
Command/Handler pattern, FluentResults error handling, transaction management, and CRUD handler patterns.

## Instructions

### Step 1: Identify the Operation Type

Use the decision tree to determine what to create:

```
What operation does the use case perform?
|
+-- CREATE (new entity)
|   -> Result<Entity>, transaction, validation errors
|   -> See references/create-handler.md
|
+-- GET (single entity by ID or unique field)
|   -> Result<Entity>, NO transaction
|   -> See references/get-handler.md
|
+-- GET MANY AND COUNT (paginated list)
|   -> GetManyAndCountResult<T>, transaction, query/sort
|   -> See references/get-many-and-count-handler.md
|
+-- UPDATE (existing entity)
|   -> Result<Entity>, transaction, not-found + validation errors
|   -> See references/update-handler.md
|
+-- DELETE (remove entity)
|   -> Result (void), transaction, not-found error
|   -> See references/delete-handler.md
|
+-- CUSTOM (non-CRUD operation)
    -> Choose closest pattern, adapt as needed
```

### Step 2: Follow the Handler Pattern

Each operation has a specific pattern documented in `references/`. Apply the pattern, including the correct Result type, transaction management, and exception handling.

### Step 3: Validate with Checklist

Use the operation-specific checklists at the end of this file to verify the generated code before finishing.

### Step 4: Create Tests

Every handler requires unit tests. See `references/application-testing.md` for the full testing guide, including base class, Moq patterns, and naming conventions. For comprehensive cross-layer testing guidance, use the `create-tests` skill.

---

## References

For complete patterns, additional examples, and edge cases, consult files in `references/`:

| Topic | Reference |
|-------|-----------|
| Use Case Structure | `references/use-case-structure.md` |
| Create Handler | `references/create-handler.md` |
| Get Handler | `references/get-handler.md` |
| GetManyAndCount Handler | `references/get-many-and-count-handler.md` |
| Update Handler | `references/update-handler.md` |
| Delete Handler | `references/delete-handler.md` |
| Error Handling | `references/error-handling.md` |
| Application Testing | `references/application-testing.md` |

---

## Application Layer Structure

```
src/{project}.application/
├── usecases/
│   ├── users/
│   │   ├── CreateUserUseCase.cs
│   │   ├── GetUserUseCase.cs
│   │   ├── GetManyAndCountUsersUseCase.cs
│   │   ├── UpdateUserUseCase.cs
│   │   └── DeleteUserUseCase.cs
│   └── prototypes/
│       ├── CreatePrototypeUseCase.cs
│       └── ...
├── common/
│   └── ValidationError.cs
└── errors/
    └── UserErrors.cs
```

---

## Use Case Anatomy

Every use case is a single class containing a nested `Command` and `Handler`. Always use primary constructor (C# 13) with `private readonly` field assignment. For the full template, see `references/use-case-structure.md`.

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| File | `{Operation}{Entity}UseCase.cs` | `CreateUserUseCase.cs` |
| Class | `{Operation}{Entity}UseCase` | `public abstract class CreateUserUseCase` |
| Command | Always `Command` | `public class Command : ICommand<Result<User>>` |
| Handler | Always `Handler` | `public class Handler(...) : ICommandHandler<Command, Result<User>>` |
| Namespace | `{project}.application.usecases.{feature}` | `hashira.stone.backend.application.usecases.users` |

### Class Modifier

- **`abstract`**: when the class contains helper methods (e.g., `GenerateRandomPassword()`)
- **`class`** (no modifier): when it only contains Command + Handler

### Result Type by Operation

| Operation | Result Type |
|-----------|------------|
| Create | `Result<Entity>` |
| Get | `Result<Entity>` |
| GetManyAndCount | `GetManyAndCountResult<Entity>` or `GetManyAndCountResult<EntityDao>` |
| Update | `Result<Entity>` |
| Delete | `Result` |

---

## Transaction Rules

| Operation | Transaction | Commit | Rollback |
|-----------|-------------|--------|----------|
| CREATE | Yes | After successful creation | In every catch block |
| GET | **No** | N/A | N/A |
| GET MANY AND COUNT | Yes | After successful query | In catch block (rethrow) |
| UPDATE | Yes | After successful update | In every catch block |
| DELETE | Yes | After successful deletion | In every catch block |

**Rules:**
1. Call `_uoW.BeginTransaction()` before any write operation
2. Call `_uoW.Commit()` only after all operations succeed
3. Call `_uoW.Rollback()` in **every** catch block that has a transaction
4. GET (single entity) does **not** need a transaction -- it is read-only
5. GetManyAndCount uses transactions because NHibernate requires it for complex queries with count

---

## Error Handling Summary

- **`Result<Entity>`**: operations that return a value (Create, Get, Update)
- **`Result`**: operations without return value (Delete)
- **`GetManyAndCountResult<T>`**: paginated list operations (not wrapped in Result)

Always catch exceptions from most specific to most general: `InvalidDomainException` (400) -> `DuplicatedDomainException` (409) -> `ResourceNotFoundException` (404) -> `HttpRequestException` (500) -> generic `Exception` (500).

Key patterns:
- **InvalidDomainException**: Message contains JSON-serialized validation errors. Deserialize with `JsonSerializer.Deserialize<List<ValidationError>>()` to extract user-friendly messages.
- **Custom error classes**: Use factory pattern (`UserErrors.UserNotFound(userName)`) for Get handlers with specific error types.
- **`CausedBy(exception)`**: preserves the original exception and stack trace
- **`WithMetadata("key", value)`**: adds debugging context (validation errors, entity IDs, timestamps)

For complete catch block templates, deserialization patterns, and custom error examples, see `references/error-handling.md`.

---

## Dependency Injection

1. Always use primary constructor (C# 13) for injection
2. Always assign parameters to `private readonly` fields
3. `IUnitOfWork` is mandatory for any data operation
4. `ILogger<Handler>` is recommended for all handlers
5. External services (`IIdentityService`, `IEmailService`, etc.) only when needed

For the full constructor pattern, see `references/use-case-structure.md`.

---

## Checklists

### Create Handler

- [ ] `BeginTransaction()` at the start
- [ ] Logging of start with identifying data
- [ ] Call to `CreateAsync()` on the repository
- [ ] `Commit()` after successful creation
- [ ] Logging of success with generated ID
- [ ] Catch `InvalidDomainException` with ValidationError deserialization
- [ ] Catch `DuplicatedDomainException`
- [ ] Catch generic `Exception`
- [ ] `Rollback()` in every catch block
- [ ] Return `Result.Fail()` on errors, `Result.Ok(entity)` on success
- [ ] CancellationToken passed to async methods

### Get Handler

- [ ] **NO** `BeginTransaction()`
- [ ] Call to `GetByIdAsync()` or equivalent method
- [ ] CancellationToken passed to async method
- [ ] Null check on returned entity
- [ ] Return `Result.Fail()` with custom error if not found
- [ ] Return `Result.Ok(entity)` if found
- [ ] Catch generic `Exception` for unexpected errors
- [ ] Logging of errors in catch

### GetManyAndCount Handler

- [ ] `BeginTransaction()` at the start
- [ ] Logging of start with query parameters
- [ ] Call to `GetManyAndCountAsync()` with query, defaultSortField, and CancellationToken
- [ ] Logging of result with count
- [ ] `Commit()` after successful query
- [ ] Catch block with `Rollback()` and rethrow (`throw;`)
- [ ] Returns `GetManyAndCountResult<T>` directly (not wrapped in Result)

### Update Handler

- [ ] `BeginTransaction()` at the start
- [ ] Logging of start with entity ID
- [ ] Call to `UpdateAsync()` on the repository
- [ ] `Commit()` after successful update
- [ ] Logging of success
- [ ] Catch `ResourceNotFoundException`
- [ ] Catch `InvalidDomainException`
- [ ] Catch `DuplicatedDomainException`
- [ ] Catch generic `Exception`
- [ ] `Rollback()` in every catch block
- [ ] Return `Result.Fail<Entity>()` on errors, `Result.Ok(updated)` on success

### Delete Handler

- [ ] `BeginTransaction()` at the start
- [ ] Logging of start with entity ID
- [ ] Call to `DeleteAsync()` on the repository
- [ ] `Commit()` after successful deletion
- [ ] Logging of success
- [ ] Catch `ResourceNotFoundException`
- [ ] Catch generic `Exception`
- [ ] `Rollback()` in every catch block
- [ ] Return `Result.Fail()` on errors, `Result.Ok()` on success

---

## Examples

### Example 1: Create a use case for creating a new Order

User says: "Create a use case for creating a new Order"

Actions:
1. Create `CreateOrderUseCase.cs` in `usecases/orders/`
2. Define `Command` with `ICommand<Result<Order>>` and properties: `OrderNumber`, `Total`, `Status`
3. Define `Handler` with primary constructor injecting `IUnitOfWork` and `ILogger<Handler>`
4. Implement `ExecuteAsync` with `BeginTransaction`, `CreateAsync`, `Commit`
5. Add catch blocks for `InvalidDomainException` (with ValidationError deserialization), `DuplicatedDomainException`, and generic `Exception`
6. Add `Rollback()` in every catch block
7. Create unit tests following `references/application-testing.md`

Result: `CreateOrderUseCase.cs` created with Command/Handler pattern, full transaction management, and error handling. Ready for endpoint mapping in WebApi layer.

### Example 2: List orders with pagination and search

User says: "List orders with pagination and search"

Actions:
1. Create `GetManyAndCountOrdersUseCase.cs` in `usecases/orders/`
2. Define `Command` with `ICommand<GetManyAndCountResult<OrderDao>>` and `Query` property
3. Define `Handler` with `BeginTransaction`, call to `GetManyAndCountAsync`
4. Pass `command.Query`, `nameof(OrderDao.OrderNumber)` as default sort, and `ct`
5. Add catch block with `Rollback()` and `throw;` (rethrow, not Result.Fail)
6. Create unit tests

Result: `GetManyAndCountOrdersUseCase.cs` created using DAO for optimized read queries. Supports search via `Query` parameter and default sort by `OrderNumber`.

### Example 3: Generate all CRUD use cases for Product

User says: "Generate all CRUD use cases for Product"

Actions:
1. Create `CreateProductUseCase.cs` -- Create pattern with `Result<Product>`
2. Create `GetProductUseCase.cs` -- Get pattern with `Result<Product>`, no transaction
3. Create `GetManyAndCountProductsUseCase.cs` -- GetManyAndCount pattern with `GetManyAndCountResult<ProductDao>`
4. Create `UpdateProductUseCase.cs` -- Update pattern with `Result<Product>`
5. Create `DeleteProductUseCase.cs` -- Delete pattern with `Result`
6. Create unit tests for each handler

Result: Five use case files created in `usecases/products/`, each following its specific pattern. All share `IUnitOfWork` injection and proper error handling.

---

## Troubleshooting

### Handler not resolved by FastEndpoints
Cause: Command and Handler are not nested inside the UseCase class, or the Handler does not implement `ICommandHandler<Command, ResultType>`.
Solution: Ensure `Command` and `Handler` are nested classes inside the use case. Verify `Handler` implements `ICommandHandler<Command, {ResultType}>` with the exact same `ResultType` as the Command's `ICommand<{ResultType}>`.

### Transaction not committed / stale data
Cause: `_uoW.Commit()` is missing or placed after the return statement.
Solution: Always call `_uoW.Commit()` inside the try block, before the `return Result.Ok()` statement. Verify the commit is reached in the happy path.

### NullReferenceException on ValidationError deserialization
Cause: `JsonSerializer.Deserialize<List<ValidationError>>(idex.Message)` returns null, and `.FirstOrDefault().ErrorMessage` is called without null-conditional operator.
Solution: Always use the null-safe pattern: `errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid data"`.

### Transaction left open after exception
Cause: A catch block is missing `_uoW.Rollback()`.
Solution: Add `_uoW.Rollback()` as the first line in **every** catch block when `BeginTransaction()` was called. This includes domain exceptions, not just the generic `Exception` catch.

### Unnecessary transaction on Get handler
Cause: Using `BeginTransaction()` / `Commit()` on a simple Get operation.
Solution: Simple Get handlers (single entity by ID) do not need transactions. Remove `BeginTransaction()`, `Commit()`, and `Rollback()`. Only GetManyAndCount requires a transaction for reads.

### Result type mismatch between Command and Handler
Cause: `Command : ICommand<Result<User>>` but `Handler : ICommandHandler<Command, Result>` (missing generic type).
Solution: The `ResultType` in `ICommand<ResultType>` and `ICommandHandler<Command, ResultType>` must be identical. If Command uses `Result<User>`, Handler must use `Result<User>` too.

### Primary constructor fields not assigned to readonly
Cause: Primary constructor parameters are used directly instead of being assigned to `private readonly` fields.
Solution: Always assign primary constructor parameters to fields: `private readonly IUnitOfWork _uoW = uoW;`. Using the parameter directly can cause unexpected behavior with closures.

---

## Related

- **Domain Layer:** `create-domain` -- Entities, validators, value objects, domain exceptions
- **Infrastructure Layer:** `create-repository` -- Repository implementations, mappers, Unit of Work
- **WebApi Layer:** `create-endpoint` -- Endpoints, request/response models, DTOs, AutoMapper profiles
- **Testing:** See `references/application-testing.md` -- Unit tests, integration tests, conventions
