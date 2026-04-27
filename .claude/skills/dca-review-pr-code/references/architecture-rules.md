# Architecture Rules Reference

Comprehensive rules for validating Clean Architecture compliance in APSYS .NET backend projects.

---

## 1. Layer Structure and Dependencies

### Clean Architecture Layers

```
WebApi (outermost)
  -> Application
    -> Domain (innermost, no dependencies)

Infrastructure (implements Domain interfaces)
  -> Domain
```

### Dependency Rules

| Layer | Can Reference | Cannot Reference |
|-------|-------------|-----------------|
| Domain | Nothing (only .NET BCL) | Application, Infrastructure, WebApi |
| Application | Domain | Infrastructure, WebApi |
| Infrastructure | Domain | Application, WebApi |
| WebApi | Application, Domain (types only) | Infrastructure (only through DI) |

### Critical Violations

```csharp
// BAD: Domain importing Infrastructure
namespace project.domain.entities;
using NHibernate;                    // VIOLATION: ORM in domain
using project.infrastructure;        // VIOLATION: infrastructure reference

// BAD: Application importing WebApi
namespace project.application.usecases.users;
using project.webapi.models;         // VIOLATION: webapi reference

// BAD: Handler accessing ISession directly
public class Handler(ISession session) // VIOLATION: bypass repository
{
    public async Task<Result<User>> ExecuteAsync(Command cmd, CancellationToken ct)
    {
        var user = await session.GetAsync<User>(cmd.Id);  // VIOLATION
    }
}
```

### Review Checks

| Check | Severity |
|-------|----------|
| Domain references Infrastructure or WebApi | Critical |
| Application references Infrastructure or WebApi | Critical |
| Handler uses ISession instead of repository | Critical |
| Entity has ORM attributes | Critical |
| Infrastructure references WebApi | Critical |

---

## 2. Project Structure

### Solution Structure

```
src/
├── {project}.domain/
│   ├── entities/
│   │   ├── User.cs
│   │   ├── Role.cs
│   │   └── validators/
│   │       ├── UserValidator.cs
│   │       └── RoleValidator.cs
│   ├── daos/
│   │   └── UserDao.cs
│   ├── exceptions/
│   │   ├── InvalidDomainException.cs
│   │   ├── DuplicatedDomainException.cs
│   │   ├── ResourceNotFoundException.cs
│   │   └── InvalidFilterArgumentException.cs
│   ├── resources/
│   │   └── UserResource.cs
│   └── interfaces/
│       ├── IUserRepository.cs
│       └── IUnitOfWork.cs
├── {project}.application/
│   └── usecases/
│       └── users/
│           ├── CreateUserUseCase.cs
│           ├── GetUserUseCase.cs
│           ├── GetManyAndCountUsersUseCase.cs
│           ├── UpdateUserUseCase.cs
│           └── DeleteUserUseCase.cs
├── {project}.infrastructure/
│   ├── mappers/
│   │   ├── UserMapper.cs
│   │   └── UserDaoMapper.cs
│   ├── repositories/
│   │   └── UserRepository.cs
│   ├── NHUnitOfWork.cs
│   └── NHSessionFactory.cs
├── {project}.webapi/
│   ├── endpoints/
│   │   └── users/
│   │       ├── CreateUserEndpoint.cs
│   │       ├── GetUserEndpoint.cs
│   │       ├── GetManyAndCountUsersEndpoint.cs
│   │       ├── UpdateUserEndpoint.cs
│   │       └── DeleteUserEndpoint.cs
│   ├── models/
│   │   └── users/
│   │       ├── CreateUserModel.cs
│   │       ├── GetUserModel.cs
│   │       ├── GetManyAndCountUsersModel.cs
│   │       ├── UpdateUserModel.cs
│   │       └── DeleteUserModel.cs
│   ├── dtos/
│   │   └── UserDto.cs
│   └── profiles/
│       └── UserMappingProfile.cs
├── {project}.migrations/
│   ├── M001Sandbox.cs
│   ├── M002CreateUserTable.cs
│   └── Program.cs
└── tests/
    ├── {project}.domain.tests/
    ├── {project}.application.tests/
    └── {project}.webapi.tests/
```

### Namespace Rules

| Layer | Namespace Pattern | Example |
|-------|------------------|---------|
| Entity | `{project}.domain.entities` | `apsys.domain.entities` |
| Validator | `{project}.domain.entities.validators` | `apsys.domain.entities.validators` |
| DAO | `{project}.domain.daos` | `apsys.domain.daos` |
| Exception | `{project}.domain.exceptions` | `apsys.domain.exceptions` |
| Resource | `{project}.domain.resources` | `apsys.domain.resources` |
| Interface | `{project}.domain.interfaces` | `apsys.domain.interfaces` |
| Use Case | `{project}.application.usecases.{feature}` | `apsys.application.usecases.users` |
| Mapper | `{project}.infrastructure.mappers` | `apsys.infrastructure.mappers` |
| Repository | `{project}.infrastructure.repositories` | `apsys.infrastructure.repositories` |
| Endpoint | `{project}.webapi.endpoints.{feature}` | `apsys.webapi.endpoints.users` |
| Model | `{project}.webapi.models.{feature}` | `apsys.webapi.models.users` |
| DTO | `{project}.webapi.dtos` | `apsys.webapi.dtos` |
| Profile | `{project}.webapi.profiles` | `apsys.webapi.profiles` |

### Review Checks

| Check | Severity |
|-------|----------|
| Namespace does not match folder structure | Warning |
| File in wrong layer folder | Critical |
| Feature folder casing inconsistency | Warning |

---

## 3. Entity Pattern

### Requirements

Every entity must follow this pattern:

```csharp
namespace {project}.domain.entities;

public class User : AbstractDomainObject
{
    // Properties: ALL virtual, with defaults
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual bool Locked { get; set; }
    public virtual IList<Role> Roles { get; set; } = new List<Role>();

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

    public override IValidator GetValidator() => new UserValidator();
}
```

### Entity Rules

| Rule | Severity |
|------|----------|
| Does not inherit AbstractDomainObject | Critical |
| Property missing `virtual` keyword | Critical |
| Missing GetValidator() override | Critical |
| Missing empty constructor (NHibernate) | Critical |
| ORM attributes on entity | Critical |
| Persistence logic in entity | Critical |
| Collection not initialized at property level | Warning |
| Missing parameterized constructor | Warning |
| Id or CreationDate in parameterized constructor | Warning |
| String property without default `string.Empty` | Warning |

### Anti-Patterns

```csharp
// BAD: ORM attributes
[Table("users")]
public class User : AbstractDomainObject { }

// BAD: Non-virtual property
public string Name { get; set; }  // Missing virtual

// BAD: Collection initialized in constructor
public User()
{
    Roles = new List<Role>();  // Should be at property level
}

// BAD: Persistence logic
public async Task SaveAsync(ISession session) { }  // VIOLATION
```

---

## 4. Use Case Pattern

### Requirements

Every use case follows the Command/Handler pattern:

```csharp
namespace {project}.application.usecases.users;

public class CreateUserUseCase
{
    public class Command : ICommand<Result<User>>
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class Handler(
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Result<User>>
    {
        public async Task<Result<User>> ExecuteAsync(Command command, CancellationToken ct)
        {
            try
            {
                unitOfWork.BeginTransaction();
                var user = new User(command.Email, command.Name);
                await unitOfWork.Users.CreateAsync(user);
                unitOfWork.Commit();
                return Result.Ok(user);
            }
            catch (InvalidDomainException ex)
            {
                return Result.Fail<User>(/* validation errors */);
            }
            catch (DuplicatedDomainException ex)
            {
                return Result.Fail<User>(ex.Message);
            }
            catch (Exception ex)
            {
                unitOfWork.Rollback();
                logger.LogError(ex, "Error creating user");
                return Result.Fail<User>(
                    new ExceptionalError(ex));
            }
        }
    }
}
```

### Use Case Rules

| Rule | Severity |
|------|----------|
| Command and Handler not nested in outer class | Critical |
| Command does not implement ICommand<Result<T>> | Critical |
| Handler does not implement ICommandHandler | Critical |
| Missing BeginTransaction for write operations | Critical |
| Missing Rollback in catch block | Critical |
| Business logic in handler (not delegated to domain) | Critical |
| GetManyAndCount returning Result.Fail instead of rethrowing | Critical |
| Wrong catch block order (generic before specific) | Warning |
| Not using primary constructor injection | Warning |
| Missing logger | Warning |

---

## 5. Endpoint Pattern

### HTTP Verb and Status Code Mapping

| Operation | HTTP Verb | Success Status | Endpoint Base |
|-----------|----------|----------------|---------------|
| Create | POST | 201 Created | `BaseEndpoint<TReq, TRes>` |
| Get | GET | 200 OK | `BaseEndpoint<TReq, TRes>` |
| GetManyAndCount | GET | 200 OK | `BaseEndpoint<TReq, TRes>` |
| Update | PUT | 200 OK | `BaseEndpoint<TReq, TRes>` |
| Delete | DELETE | 204 No Content | `Endpoint<TReq>` or `BaseEndpoint<TReq, object>` |

### Error Handling in Endpoints

```csharp
// Domain exception to HTTP status mapping
switch (result.Errors.First())
{
    case ExceptionalError { Exception: InvalidDomainException ex }:
        SendBadRequestAsync(ex);       // 400
        break;
    case ExceptionalError { Exception: DuplicatedDomainException ex }:
        SendConflictAsync(ex);          // 409
        break;
    case ExceptionalError { Exception: ResourceNotFoundException ex }:
        SendNotFoundAsync(ex);          // 404
        break;
    default:
        SendInternalErrorAsync(result); // 500
        break;
}
```

### Exception-to-Status Mapping per Operation

| Operation | Possible Exceptions | HTTP Status |
|-----------|-------------------|-------------|
| Create | InvalidDomainException | 400 |
| Create | DuplicatedDomainException | 409 |
| Get | ResourceNotFoundException (custom error) | 404 |
| GetManyAndCount | Exception (try-catch) | 500 |
| Update | ResourceNotFoundException | 404 |
| Update | InvalidDomainException | 400 |
| Update | DuplicatedDomainException | 409 |
| Delete | ResourceNotFoundException | 404 |

### Request Validation

Every request model that receives user input must have a corresponding FluentValidation validator:

```csharp
// GOOD: Request validator for create endpoint
public class CreateUserRequestValidator : Validator<CreateUserModel.Request>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Email] cannot be null or empty")
            .WithErrorCode("Email");

        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Name] cannot be null or empty")
            .WithErrorCode("Name");
    }
}

// GOOD: Request validator for route parameter endpoints
public class GetUserRequestValidator : Validator<GetUserModel.Request>
{
    public GetUserRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("The [Id] cannot be empty")
            .WithErrorCode("Id");
    }
}
```

```csharp
// BAD: No request validator exists for endpoint
// File: endpoints/users/CreateUserEndpoint.cs — uses CreateUserModel.Request
// File: validators/CreateUserRequestValidator.cs — DOES NOT EXIST
// VIOLATION: user input reaches handler without validation
```

### Review Checks

| Check | Severity |
|-------|----------|
| Endpoint returns wrong HTTP status for exception type | Critical |
| Missing error handling for expected exception | Critical |
| Missing FluentValidation request validator for endpoint | Warning |
| Create endpoint returns 200 instead of 201 | Warning |
| Delete endpoint returns response body | Warning |
| Missing DontThrowIfValidationFails() on create/update | Warning |
| Missing Swagger error documentation | Suggestion |

---

## 6. Request/Response Model Pattern

### Requirements

```csharp
namespace {project}.webapi.models.users;

public class CreateUserModel
{
    public class Request
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class Response
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
    }
}
```

### Model Rules

| Rule | Severity |
|------|----------|
| Request/Response not nested in Model class | Warning |
| Missing default initialization on string properties | Warning |
| Model naming does not follow {Verb}{Entity}Model pattern | Warning |
| DTO exposes internal domain details | Warning |

---

## 7. AutoMapper Profile Pattern

### Required Mapping Directions

| Direction | When | Example |
|-----------|------|---------|
| Entity -> DTO | Listing endpoints | `CreateMap<User, UserDto>()` |
| DAO -> DTO | Paginated listing | `CreateMap<UserDao, UserDto>()` |
| Request -> Command | Endpoint -> Handler | `CreateMap<CreateUserModel.Request, CreateUserUseCase.Command>()` |
| Entity -> Response | Create/Update endpoints | `CreateMap<User, CreateUserModel.Response>()` with ForMember |

### Review Checks

| Check | Severity |
|-------|----------|
| Missing mapping for a direction used in endpoint | Critical |
| Missing ForMember for non-trivial property mapping | Warning |
| Profile not following naming convention | Suggestion |

---

## 8. Migration Pattern

### Requirements

```csharp
[Migration(002)]
public class M002CreateUserTable : Migration
{
    public override void Up()
    {
        Create.Table("users").InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("email").AsString(255).NotNullable().Unique()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("creation_date").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("users").InSchema("public");
    }
}
```

### Migration Rules

| Rule | Severity |
|------|----------|
| Missing Down() implementation | Warning |
| Non-sequential version number | Warning |
| Destructive operation without documentation | Critical |
| Hardcoded secrets in seed data | Critical |
| Non-idempotent seed (missing ON CONFLICT) | Warning |
| Class name does not match version number (M###) | Suggestion |
