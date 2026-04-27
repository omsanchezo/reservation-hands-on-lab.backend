# Code Organization

## File-Scoped Namespaces (C# 10+)

```csharp
// GOOD: file-scoped namespace
namespace Domain.Entities;

public class User
{
    public virtual Guid Id { get; protected set; }
    public virtual string Email { get; set; }
}
```

```csharp
// BAD: block-scoped namespace (unnecessary indentation)
namespace Domain.Entities
{
    public class User
    {
        public virtual Guid Id { get; protected set; }
        public virtual string Email { get; set; }
    }
}
```

## Namespace Conventions by Layer

### Domain Layer: `Domain.{Functionality}`

```csharp
namespace Domain.Entities;
namespace Domain.Entities.Validators;
namespace Domain.ValueObjects;
namespace Domain.Interfaces.Repositories;
namespace Domain.Interfaces.Services;
namespace Domain.Daos;
namespace Domain.Exceptions;
namespace Domain.Enums;
```

### Application Layer: `Application.{Functionality}.{Feature}`

```csharp
namespace Application.UseCases.Users;
namespace Application.UseCases.Products;
namespace Application.Dtos;
namespace Application.Interfaces;
```

### Infrastructure Layer: `Infrastructure.{Technology}.{Functionality}`

```csharp
namespace Infrastructure.NHibernate;
namespace Infrastructure.NHibernate.Mappers;
namespace Infrastructure.NHibernate.Repositories;
namespace Infrastructure.ExternalServices.Email;
```

### WebApi Layer: `WebApi.{Functionality}.{Feature}`

```csharp
namespace WebApi.Features.Users.Endpoint;
namespace WebApi.Features.Users.Models;
namespace WebApi.Dtos;
namespace WebApi.Middleware;
```

## Using Directives Order

1. **System namespaces** (alphabetical)
2. **Blank line**
3. **Third-party namespaces** (alphabetical)
4. **Blank line**
5. **Project namespaces** (alphabetical, by layer)

```csharp
namespace Application.UseCases.Users;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using FluentResults;
using FluentValidation;
using MediatR;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
```

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Namespaces | PascalCase | `Domain.Entities` |
| Classes | PascalCase | `User`, `UserValidator` |
| Interfaces | IPascalCase | `IUserRepository` |
| Methods | PascalCase | `GetUserById` |
| Properties | PascalCase | `Email`, `FullName` |
| Private fields | _camelCase | `_unitOfWork`, `_session` |
| Parameters | camelCase | `userId`, `email` |
| Local variables | camelCase | `user`, `result` |
| Constants | PascalCase | `MaxRetries`, `DefaultTimeout` |
| Enums/values | PascalCase | `UserRole`, `Active` |
| Folders | kebab-case | `use-cases`, `users` |

```csharp
// BAD
public interface UserService { }             // Missing I prefix
private readonly IUnitOfWork unitOfWork;     // Missing _ prefix
private const int MAX_RETRIES = 3;           // SCREAMING_CASE
public async Task<User?> getUserById() { }  // camelCase method
```

## File Naming by Type

| Type | Pattern | Example |
|------|---------|---------|
| Entity | `{Entity}.cs` | `User.cs` |
| Validator | `{Entity}Validator.cs` | `UserValidator.cs` |
| Repository Interface | `I{Entity}Repository.cs` | `IUserRepository.cs` |
| Repository Impl | `NH{Entity}Repository.cs` | `NHUserRepository.cs` |
| Mapper | `{Entity}Mapper.cs` | `UserMapper.cs` |
| DAO | `{Entity}Dao.cs` | `UserDao.cs` |
| UseCase | `{Action}{Entity}UseCase.cs` | `CreateUserUseCase.cs` |
| Endpoint | `{Action}{Entity}Endpoint.cs` | `CreateUserEndpoint.cs` |
| Model | `{Action}{Entity}Model.cs` | `CreateUserModel.cs` |
| DTO | `{Entity}Dto.cs` | `UserDto.cs` |

## One File, One Responsibility

Each file contains **one** class, interface, record, or enum.

```csharp
// BAD: multiple unrelated entities in one file
namespace Domain.Entities;
public class User { ... }
public class Product { ... }
public class Order { ... }
```

**Exception**: strongly related nested types are acceptable:

```csharp
// OK: Request/Response in same file
public static class CreateUserModel
{
    public record Request(string Email, string FullName);
    public record Response(Guid UserId);
}

// OK: Command/Handler in same file
public record CreateUserCommand(string Email) : IRequest<Result<User>>;
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<User>> { ... }
```

## Folder Organization by Feature (Vertical Slicing)

```
// GOOD: organized by feature
Application/UseCases/
    Users/
        CreateUserUseCase.cs
        GetUserUseCase.cs
        UpdateUserUseCase.cs
    Products/
        CreateProductUseCase.cs
        GetProductUseCase.cs

// BAD: organized by action type (horizontal)
Application/UseCases/
    Create/
        CreateUserUseCase.cs
        CreateProductUseCase.cs
    Get/
        GetUserUseCase.cs
        GetProductUseCase.cs
```

## Code Formatting Rules

```csharp
// 4 spaces indentation (not tabs)
// Blank line between usings and class
// Blank line between fields and constructor
// Blank line between methods
// Allman style braces (new line)
// Always use braces for if/for/while (even single-line)
// Max 120 characters per line

public class UserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<User?> GetUserAsync(Guid id, CancellationToken ct)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
        return user;
    }
}

// GOOD: braces on single-line blocks
if (user == null)
{
    throw new ArgumentNullException(nameof(user));
}

// BAD: omitting braces
if (user == null)
    throw new ArgumentNullException(nameof(user));

// GOOD: break long parameter lists
public async Task<Result<User>> CreateUserAsync(
    string email,
    string fullName,
    UserRole role,
    CancellationToken ct)
```

## Checklist: New File

- [ ] File-scoped namespace (C# 10+)
- [ ] Usings ordered: System -> Third-party -> Project
- [ ] Blank lines between using groups
- [ ] One class/interface/record per file
- [ ] File name matches the main class
- [ ] Classes use PascalCase, interfaces have I prefix
- [ ] Private fields use _camelCase
- [ ] Parameters and variables use camelCase
- [ ] 4-space indentation, no tabs
- [ ] Blank lines between methods
- [ ] Allman style braces
- [ ] Namespace reflects Clean Architecture layer
- [ ] File placed in correct feature folder

## Checklist: Code Review

- [ ] Namespaces consistent with project structure
- [ ] Usings correctly ordered, no unnecessary usings
- [ ] Descriptive class, method, and parameter names
- [ ] No files with multiple unrelated classes
- [ ] Organization is by feature, not by technical type
- [ ] Private fields have underscore prefix
- [ ] Constants use PascalCase (not SCREAMING_CASE)
- [ ] No naming convention warnings
