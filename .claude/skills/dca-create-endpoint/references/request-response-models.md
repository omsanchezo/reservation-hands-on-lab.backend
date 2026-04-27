# Request/Response Models Reference

## Nested Class Pattern

Every endpoint operation uses a single outer class containing nested `Request` and `Response` classes:

```csharp
using {project}.webapi.dtos;

namespace {project}.webapi.features.{feature}.models;

/// <summary>
/// Data model for {operation description}
/// </summary>
public class {Verb}{Entity}Model
{
    /// <summary>
    /// Represents the request data used to {operation} {entity}
    /// </summary>
    public class Request
    {
        /// <summary>
        /// Gets or sets the {property description}.
        /// </summary>
        public {Type} {Property} { get; set; } = {default};
    }

    /// <summary>
    /// Represents the response data returned after {operation} {entity}
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets or sets the {entity} data.
        /// </summary>
        public {Entity}Dto {Entity} { get; set; } = new();
    }
}
```

## Naming Convention

Pattern: `{Verb}{Entity}Model`

| Verb | HTTP Method | Example |
|------|-------------|---------|
| `Create` | POST | `CreateUserModel` |
| `Get` | GET | `GetUserModel` |
| `GetManyAndCount` | GET | `GetManyAndCountUsersModel` |
| `Update` | PUT | `UpdateUserModel` |
| `Delete` | DELETE | `DeleteUserModel` |

Inner classes are **always** named `Request` and `Response`. Never use `Req`, `Res`, `Input`, `Output`.

## Models by Operation Type

### Create Model

```csharp
/// <summary>
/// Data model for creating a new user
/// </summary>
public class CreateUserModel
{
    /// <summary>
    /// Represents the request data used to create a new user
    /// </summary>
    public class Request
    {
        /// <summary>
        /// Gets or sets the Name of the user.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Email of the user.
        /// </summary>
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the response data returned after creating a new user.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets or sets the newly created user.
        /// </summary>
        public UserDto User { get; set; } = new UserDto();
    }
}
```

### Get Model

```csharp
/// <summary>
/// Data model for retrieving a user by username
/// </summary>
public class GetUserModel
{
    /// <summary>
    /// Represents the request data used to get a user by username
    /// </summary>
    public class Request
    {
        /// <summary>
        /// Gets or sets the username of the user to retrieve.
        /// This value is extracted from the route parameter.
        /// </summary>
        public string UserName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response containing the requested user
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets or sets the requested user data.
        /// </summary>
        public UserDto User { get; set; } = new UserDto();
    }
}
```

### GetManyAndCount Model

The Request is typically empty because the query string is passed directly via `HttpContext.Request.QueryString.Value`. The Response inherits from `GetManyAndCountResultDto<T>`:

```csharp
/// <summary>
/// Data model for retrieving a paginated list of users
/// </summary>
public class GetManyAndCountUsersModel
{
    /// <summary>
    /// Request model for retrieving a paginated list of users.
    /// Query parameters are extracted from HttpContext.Request.QueryString.
    /// </summary>
    public class Request
    {
        // Empty -- query params obtained via HttpContext.Request.QueryString.Value
    }

    /// <summary>
    /// Represents a paginated list of users along with the total count.
    /// </summary>
    public class Response : GetManyAndCountResultDto<UserDto>
    {
        // Inherits: Items, Count, PageNumber, PageSize, SortBy, SortCriteria
    }
}
```

### Update Model

Request combines route parameter (`Id`) with body properties:

```csharp
/// <summary>
/// Data model for updating an existing technical standard
/// </summary>
public class UpdateTechnicalStandardModel
{
    /// <summary>
    /// Represents the request data used to update an existing technical standard
    /// </summary>
    public class Request
    {
        /// <summary>
        /// Gets or sets the unique identifier of the technical standard to update.
        /// This value is extracted from the route parameter.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the code of the technical standard.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the technical standard.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the response data returned after updating a technical standard.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets or sets the updated technical standard.
        /// </summary>
        public TechnicalStandardDto TechnicalStandard { get; set; } = new();
    }
}
```

### Delete Model

Delete has only a Request (no Response -- returns 204 No Content):

```csharp
/// <summary>
/// Data model for deleting a technical standard
/// </summary>
public class DeleteTechnicalStandardModel
{
    /// <summary>
    /// Represents the request data used to delete a technical standard
    /// </summary>
    public class Request
    {
        /// <summary>
        /// Gets or sets the unique identifier of the technical standard to delete.
        /// This value is extracted from the route parameter.
        /// </summary>
        public Guid Id { get; set; }
    }
}
```

## DTOs

DTOs are simple POCO classes in `{project}.webapi.dtos`. They contain **only properties**, no logic:

```csharp
namespace {project}.webapi.dtos;

/// <summary>
/// Data Transfer Object for {Entity} information
/// </summary>
public class {Entity}Dto
{
    /// <summary>
    /// The unique identifier of the {entity}
    /// </summary>
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    // ... only properties, no methods
}
```

### GetManyAndCountResultDto<T>

Shared generic DTO for paginated responses:

```csharp
namespace {project}.webapi.dtos;

/// <summary>
/// Data transfer object for GetManyAndCountResult<T> class
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class GetManyAndCountResultDto<T>
{
    /// <summary>
    /// Gets or sets the collection of items for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// Gets or sets the total count of records that match the query criteria.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based indexing).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the name of the field used for sorting.
    /// </summary>
    public string SortBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort direction (e.g., "asc" or "desc").
    /// </summary>
    public string SortCriteria { get; set; } = string.Empty;
}
```

## Property Initialization Rules

| Type | Initialization | Example |
|------|---------------|---------|
| `string` | `= string.Empty` | `public string Name { get; set; } = string.Empty;` |
| `string?` | No init (nullable) | `public string? Query { get; set; }` |
| `IEnumerable<T>` | `= Enumerable.Empty<T>()` | `public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();` |
| DTO references | `= new()` | `public UserDto User { get; set; } = new();` |
| Value types | Default value or no init | `public Guid Id { get; set; }` |

## File Location

```
src/{project}.webapi/
├── features/
│   └── {feature}/
│       └── models/
│           ├── Create{Entity}Model.cs
│           ├── Get{Entity}Model.cs
│           ├── GetManyAndCount{Entities}Model.cs
│           ├── Update{Entity}Model.cs
│           └── Delete{Entity}Model.cs
└── dtos/
    ├── {Entity}Dto.cs
    └── GetManyAndCountResultDto.cs
```

## Anti-Patterns

```csharp
// NEVER: Expose domain entities in Response
public class Response { public User User { get; set; } }  // Use UserDto

// NEVER: Add logic to models
public class Request { public bool IsValid() { ... } }  // Logic-free

// NEVER: Leave strings uninitialized
public string Name { get; set; }  // Use = string.Empty

// NEVER: Use List<T> in DTOs
public List<UserDto> Users { get; set; }  // Use IEnumerable<T>

// NEVER: Separate Request/Response into different files
// Keep nested inside the Model class
```
