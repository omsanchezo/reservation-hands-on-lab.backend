# C# Rules Reference

Comprehensive C# conventions for reviewing APSYS .NET backend code.

---

## 1. Naming Conventions

### General Rules

| Element | Convention | Example |
|---------|-----------|---------|
| Class | PascalCase | `UserRepository`, `CreateUserUseCase` |
| Interface | `I` + PascalCase | `IUserRepository`, `IUnitOfWork` |
| Method | PascalCase | `GetAsync`, `CreateAsync`, `ExecuteAsync` |
| Property | PascalCase | `Email`, `CreationDate`, `IsValid` |
| Local variable | camelCase | `user`, `validationResult`, `existingEntity` |
| Parameter | camelCase | `email`, `cancellationToken`, `command` |
| Constant | PascalCase | `MaxRetries`, `DefaultPageSize` |
| Static field | PascalCase | `ValidStatuses`, `AllowedRoles` |
| Private field | `_camelCase` | `_logger`, `_unitOfWork` |
| Namespace | PascalCase, dot-separated | `project.domain.entities` |
| Enum value | PascalCase | `Active`, `Inactive`, `Pending` |

### Async Method Naming

All async methods must end with `Async`:

```csharp
// GOOD
public async Task<User> GetAsync(Guid id) { }
public async Task<User> CreateAsync(User entity) { }
public async Task DeleteAsync(User entity) { }

// BAD
public async Task<User> Get(Guid id) { }        // Missing Async suffix
public async Task<User> CreateUser(User entity) { } // Missing Async suffix
```

### Boolean Naming

Boolean properties and variables should use descriptive prefixes:

```csharp
// GOOD
public virtual bool IsDeleted { get; set; }
public virtual bool HasPermission { get; set; }
public virtual bool CanEdit { get; set; }
public virtual bool Locked { get; set; }  // Acceptable for simple states

// BAD
public virtual bool Delete { get; set; }     // Ambiguous: verb or adjective?
public virtual bool Permission { get; set; }  // Missing prefix
```

### Use Case Naming

```csharp
// Pattern: {Operation}{Entity}UseCase
CreateUserUseCase.cs
GetUserUseCase.cs
GetManyAndCountUsersUseCase.cs  // Plural for list operations
UpdateUserUseCase.cs
DeleteUserUseCase.cs
```

### Endpoint Naming

```csharp
// Pattern: {Operation}{Entity}Endpoint
CreateUserEndpoint.cs
GetUserEndpoint.cs
GetManyAndCountUsersEndpoint.cs
UpdateUserEndpoint.cs
DeleteUserEndpoint.cs
```

### Model Naming

```csharp
// Pattern: {Operation}{Entity}Model
CreateUserModel.cs       // Contains nested Request and Response
GetUserModel.cs
GetManyAndCountUsersModel.cs
UpdateUserModel.cs
DeleteUserModel.cs       // Contains only Request (no response body)
```

### Review Checks

| Check | Severity |
|-------|----------|
| Async method missing `Async` suffix | Warning |
| Class/method not PascalCase | Warning |
| Local variable not camelCase | Warning |
| Interface missing `I` prefix | Warning |
| Use case naming does not match pattern | Warning |
| Namespace does not match folder structure | Warning |

---

## 2. Constructor Patterns

### Primary Constructor Injection (Preferred - C# 12+)

```csharp
// GOOD: Primary constructor (C# 12+)
public class Handler(
    IUnitOfWork unitOfWork,
    ILogger<Handler> logger) : ICommandHandler<Command, Result<User>>
{
    public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
    {
        unitOfWork.BeginTransaction();
        // ...
        logger.LogError(ex, "Error message");
    }
}

// ACCEPTABLE: Traditional constructor injection
public class Handler : ICommandHandler<Command, Result<User>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<Handler> _logger;

    public Handler(IUnitOfWork unitOfWork, ILogger<Handler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
}
```

### Entity Constructors

```csharp
// GOOD: Two required constructors
public class User : AbstractDomainObject
{
    /// <summary>
    /// Constructor used by NHibernate for mapping purposes.
    /// </summary>
    public User() { }

    /// <summary>
    /// Initializes a new instance with the specified values.
    /// </summary>
    public User(string email, string name)
    {
        Email = email;
        Name = name;
    }
}

// BAD: Missing empty constructor
public class User : AbstractDomainObject
{
    public User(string email, string name) { }  // NHibernate will fail
}

// BAD: Id in parameterized constructor
public User(Guid id, string email)  // Id is auto-assigned
{
    Id = id;  // VIOLATION
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Entity missing empty constructor | Critical |
| Entity with Id in parameterized constructor | Warning |
| Handler not using primary constructor injection | Suggestion |
| Handler constructor with too many dependencies (>5) | Suggestion |

---

## 3. Property Patterns

### Virtual Keyword (NHibernate Requirement)

All entity and DAO properties must be `virtual`:

```csharp
// GOOD
public virtual string Email { get; set; } = string.Empty;
public virtual IList<Role> Roles { get; set; } = new List<Role>();

// BAD: Missing virtual
public string Email { get; set; } = string.Empty;  // NHibernate proxy failure
```

### Default Initialization

```csharp
// GOOD: String properties with default
public virtual string Email { get; set; } = string.Empty;
public virtual string Name { get; set; } = string.Empty;

// GOOD: Collection properties with default
public virtual IList<Role> Roles { get; set; } = new List<Role>();

// GOOD: Nullable reference type (no default needed)
public virtual string? MiddleName { get; set; }

// GOOD: Value types (implicit defaults)
public virtual bool Locked { get; set; }
public virtual int Count { get; set; }
public virtual decimal Total { get; set; }
public virtual DateTime CreationDate { get; set; }

// BAD: Uninitialized string
public virtual string Email { get; set; }  // Could be null
```

### Review Checks

| Check | Severity |
|-------|----------|
| Entity/DAO property missing `virtual` | Critical |
| String property without default initialization | Warning |
| Collection property without initialization | Warning |
| Nullable type without ? annotation | Warning |

---

## 4. Value Object Pattern

### Sealed Record with Init

```csharp
// GOOD: Value object as sealed record
public sealed record Email
{
    public string Value { get; init; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format");
        Value = value;
    }
}

// BAD: Mutable value object
public class Email
{
    public string Value { get; set; }  // Mutable! Value objects must be immutable
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Value object not `sealed record` | Warning |
| Value object with `set` instead of `init` | Warning |
| Value object without constructor validation | Warning |
| Value object inheriting AbstractDomainObject | Critical |

---

## 5. FluentValidation Conventions

### Rule Structure

Every validator rule must include `WithMessage()` and `WithErrorCode()`:

```csharp
// GOOD: Complete validation rule
RuleFor(x => x.Email)
    .NotNull()
    .NotEmpty()
    .WithMessage("The [Email] cannot be null or empty")
    .WithErrorCode("Email")
    .EmailAddress()
    .WithMessage("The [Email] is not a valid email address")
    .WithErrorCode("Email_InvalidDomain");

// BAD: Missing WithErrorCode
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("The [Email] cannot be empty");  // Missing WithErrorCode

// BAD: Missing WithMessage
RuleFor(x => x.Email)
    .NotEmpty()
    .WithErrorCode("Email");  // Missing WithMessage
```

### Error Code Naming

| Pattern | Usage | Example |
|---------|-------|---------|
| `"PropertyName"` | Default validation | `"Email"`, `"Name"` |
| `"PropertyName_SpecificError"` | Additional validation | `"Email_InvalidDomain"`, `"Status_InvalidValue"` |

### Resource Classes for Constants

```csharp
// GOOD: Constants in resource class
public static class UserResource
{
    public const string StatusActive = "Active";
    public const string StatusInactive = "Inactive";
    public static readonly string[] ValidStatuses = { StatusActive, StatusInactive };
}

// BAD: Magic strings in validator
RuleFor(x => x.Status)
    .Must(s => new[] { "Active", "Inactive" }.Contains(s));  // Magic strings
```

### Review Checks

| Check | Severity |
|-------|----------|
| Validator rule missing `WithMessage()` | Warning |
| Validator rule missing `WithErrorCode()` | Warning |
| Magic strings instead of resource constants | Warning |
| Validator not linked to entity via GetValidator() | Critical |

---

## 6. FluentResults Conventions

### Result Types

```csharp
// GOOD: Use Result<T> for operations returning data
public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)

// GOOD: Use Result (no generic) for void operations
public async Task<Result> ExecuteAsync(Command command, CancellationToken ct)

// BAD: Returning entity directly (no error handling)
public async Task<User> ExecuteAsync(Command command, CancellationToken ct)
```

### Exception-to-Result Conversion

```csharp
// GOOD: Correct catch block order and conversion
try
{
    // ... operation
    return Result.Ok(entity);
}
catch (InvalidDomainException ex)
{
    var errors = JsonSerializer.Deserialize<List<ValidationError>>(ex.Message);
    return Result.Fail<User>(errors.Select(e =>
        new FluentResults.Error(e.ErrorMessage)
            .WithMetadata("ErrorCode", e.ErrorCode)
            .WithMetadata("PropertyName", e.PropertyName)));
}
catch (DuplicatedDomainException ex)
{
    return Result.Fail<User>(ex.Message);
}
catch (ResourceNotFoundException ex)
{
    return Result.Fail<User>(ex.Message);
}
catch (Exception ex)
{
    unitOfWork.Rollback();
    logger.LogError(ex, "Unexpected error");
    return Result.Fail<User>(new ExceptionalError(ex));
}
```

### GetManyAndCount Exception (No Result.Fail)

```csharp
// GOOD: GetManyAndCount rethrows
catch (Exception ex)
{
    unitOfWork.Rollback();
    logger.LogError(ex, "Error listing entities");
    throw;  // Rethrow, do NOT return Result.Fail
}

// BAD: GetManyAndCount returns Result.Fail
catch (Exception ex)
{
    return Result.Fail<GetManyAndCountResult<UserDao>>(  // VIOLATION
        new ExceptionalError(ex));
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Handler returns entity directly without Result wrapper | Critical |
| Wrong catch block order (generic before specific) | Warning |
| Missing ExceptionalError for generic Exception catch | Warning |
| GetManyAndCount using Result.Fail instead of throw | Critical |
| Missing Rollback in generic Exception catch | Critical |
| Missing CausedBy for ExceptionalError | Suggestion |

---

## 7. XML Documentation

### Required Documentation

```csharp
// GOOD: Entity constructors documented
/// <summary>
/// Constructor used by NHibernate for mapping purposes.
/// </summary>
public User() { }

/// <summary>
/// Initializes a new instance with the specified values.
/// </summary>
public User(string email, string name) { }

// GOOD: Repository custom methods documented
/// <summary>
/// Retrieves a user by email address.
/// </summary>
/// <param name="email">The email to search for.</param>
/// <returns>The user if found, null otherwise.</returns>
public async Task<User?> GetByEmailAsync(string email) { }
```

### Documentation Language

All documentation must be in **English**:

```csharp
// GOOD: English documentation
/// <summary>
/// Retrieves a user by email address.
/// </summary>

// BAD: Spanish documentation
/// <summary>
/// Obtiene un usuario por su correo electronico.
/// </summary>
```

### Review Checks

| Check | Severity |
|-------|----------|
| Documentation in Spanish | Warning |
| Missing docs on public API | Suggestion |
| Missing docs on entity constructors | Suggestion |
| Missing docs on complex public methods | Suggestion |

---

## 8. Code Quality Rules

### No Magic Strings/Numbers

```csharp
// GOOD: Named constants
public static class UserResource
{
    public const int MaxNameLength = 100;
    public const string StatusActive = "Active";
}

RuleFor(x => x.Name).MaximumLength(UserResource.MaxNameLength);

// BAD: Magic values
RuleFor(x => x.Name).MaximumLength(100);  // What is 100?
```

### No Commented-Out Code

```csharp
// BAD: Commented-out code without reason
// var oldUser = await repo.GetAsync(id);
// await repo.DeleteAsync(oldUser);
var user = await repo.GetAsync(id);

// ACCEPTABLE: With ticket reference
// TODO: KC-450 - Revert to soft delete after migration
await repo.DeleteAsync(user);
```

### Var Usage

```csharp
// GOOD: Type obvious from right side
var user = new User("email@test.com", "Test");
var users = await unitOfWork.Users.GetManyAndCountAsync(expression, page, size);

// GOOD: Type explicitly stated
User user = GetUserFromSomeComplexMethod();

// BAD: Ambiguous var
var result = ProcessData();  // What type is result?
```

### Pattern Matching

```csharp
// GOOD: Pattern matching for type checks
if (error is ExceptionalError { Exception: InvalidDomainException ex })
{
    // handle
}

// GOOD: Switch expression
var status = exception switch
{
    InvalidDomainException => 400,
    ResourceNotFoundException => 404,
    DuplicatedDomainException => 409,
    _ => 500,
};
```

### Review Checks

| Check | Severity |
|-------|----------|
| Magic numbers without named constants | Warning |
| Commented-out code without reason | Warning |
| TODO without ticket reference | Warning |
| Ambiguous `var` usage | Suggestion |
| Could use pattern matching but doesn't | Suggestion |

---

## 9. Error Handling Rules

### Catch Block Order

Always catch specific exceptions first, generic last:

```csharp
// GOOD: Specific to generic
catch (InvalidDomainException ex) { }     // 1. Most specific
catch (DuplicatedDomainException ex) { }  // 2. Specific
catch (ResourceNotFoundException ex) { }  // 3. Specific
catch (Exception ex) { }                  // 4. Generic (last)

// BAD: Generic first
catch (Exception ex) { }                  // VIOLATION: catches everything
catch (InvalidDomainException ex) { }     // Never reached!
```

### Transaction Safety

```csharp
// GOOD: Rollback in catch, Commit on success
try
{
    unitOfWork.BeginTransaction();
    // ... operations
    unitOfWork.Commit();
    return Result.Ok(entity);
}
catch (Exception ex)
{
    unitOfWork.Rollback();
    // ...
}

// BAD: Missing Rollback
catch (Exception ex)
{
    logger.LogError(ex, "Error");
    return Result.Fail<User>(new ExceptionalError(ex));
    // VIOLATION: Transaction left dangling
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Generic Exception caught before specific | Warning |
| Missing Rollback after BeginTransaction | Critical |
| Empty catch block | Critical |
| Swallowing exception (catch without log or rethrow) | Warning |
| Logging exception but not including it in Result | Warning |

---

## 10. Import/Using Organization

### Using Statement Order

```csharp
// GOOD: Organized usings
using System;
using System.Collections.Generic;
using System.Text.Json;

using FluentResults;
using FluentValidation;
using NHibernate;

using project.domain.entities;
using project.domain.exceptions;
using project.domain.interfaces;
```

### Review Checks

| Check | Severity |
|-------|----------|
| Unused using statements | Suggestion |
| Mixed using order | Suggestion |
| Missing required using for type used | Warning |

---

## 11. Date Handling Conventions

All dates in APSYS projects are stored and compared in UTC. Frontends send `DateTimeOffset`, and the backend converts to UTC before persisting.

### Layer-by-Layer Rules

| Layer | Rule |
|-------|------|
| Request Models | Use `DateTimeOffset` to receive dates from frontend |
| AutoMapper Profile | Convert to UTC: `.UtcDateTime` or `.ToUniversalTime()` |
| Entities | Store dates as `DateTime` in UTC |
| Repositories/Queries | Compare against `DateTime.UtcNow` |
| DTOs/Responses | Return dates in UTC (serialized with `Z` suffix) |

### Code Examples

```csharp
// GOOD: Request model uses DateTimeOffset
public class CreateEventModel
{
    public class Request
    {
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
    }
}

// GOOD: AutoMapper profile converts to UTC
CreateMap<CreateEventModel.Request, CreateEventUseCase.Command>()
    .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate.UtcDateTime))
    .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate.UtcDateTime));

// GOOD: Entity stores DateTime (UTC)
public class Event : AbstractDomainObject
{
    public virtual DateTime StartDate { get; set; }
    public virtual DateTime EndDate { get; set; }
}

// GOOD: Repository query uses DateTime.UtcNow
var activeEvents = await Session.Query<Event>()
    .Where(e => e.EndDate > DateTime.UtcNow)
    .ToListAsync();
```

```csharp
// BAD: Request model uses DateTime instead of DateTimeOffset
public class Request
{
    public DateTime StartDate { get; set; }  // VIOLATION: loses timezone info from frontend
}

// BAD: No UTC conversion in AutoMapper profile
CreateMap<Request, Command>()
    .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate));
    // VIOLATION: date may not be UTC

// BAD: Using DateTime.Now instead of DateTime.UtcNow
var expired = await Session.Query<Event>()
    .Where(e => e.EndDate < DateTime.Now)  // VIOLATION: server-local time, not UTC
    .ToListAsync();
```

### Review Checks

| Check | Severity |
|-------|----------|
| Request model uses `DateTime` instead of `DateTimeOffset` for frontend dates | Warning |
| AutoMapper profile missing UTC conversion (`.UtcDateTime` / `.ToUniversalTime()`) | Warning |
| Date comparison uses `DateTime.Now` instead of `DateTime.UtcNow` | Warning |
| Repository query uses `DateTime.Now` as reference | Warning |
| Entity stores date without UTC convention documented | Suggestion |
