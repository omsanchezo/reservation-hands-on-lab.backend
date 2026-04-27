---
name: create-endpoint
description: >-
  Guides creation of FastEndpoints WebApi layer components in APSYS .NET backend
  projects. Covers endpoint classes (Create, Get, GetManyAndCount, Update, Delete),
  request/response models, DTOs, AutoMapper profiles, BaseEndpoint error handling,
  and domain-to-HTTP error mapping. Use when user asks to "create an endpoint",
  "add a REST API", "create request/response models", "add a mapping profile",
  "handle endpoint errors", "map domain exceptions to HTTP status codes",
  or "write endpoint tests" in Clean Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with FastEndpoints,
  FluentResults, AutoMapper, and NHibernate. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 2.0.0
---

# FastEndpoints Endpoint Creation Skill

Guide for creating and maintaining FastEndpoints WebApi layer components in APSYS .NET backend projects:
endpoint classes, request/response models, DTOs, AutoMapper profiles, error handling, and endpoint testing.

## Instructions

### Step 1: Identify the Operation Type

Use the decision tree to determine what to create:

```
What endpoint operation are you building?
|
+-- CREATE (new entity)
|   -> POST, 201 Created, validation + duplicate errors
|   -> See references/create-endpoint.md
|
+-- GET (single entity by ID or unique field)
|   -> GET /{entity}/{id}, 200 OK, not-found error
|   -> See references/get-endpoint.md
|
+-- GET MANY AND COUNT (paginated list)
|   -> GET /{entities}, 200 OK, query string passthrough
|   -> See references/get-many-endpoint.md
|
+-- UPDATE (existing entity)
|   -> PUT /{entity}/{id}, 200 OK, not-found + validation + duplicate errors
|   -> See references/update-endpoint.md
|
+-- DELETE (remove entity)
|   -> DELETE /{entity}/{id}, 204 No Content, not-found error
|   -> See references/delete-endpoint.md
|
+-- CUSTOM (non-CRUD operation)
    -> Choose closest CRUD pattern, adapt as needed
```

### Step 2: Create All Required Components

For each endpoint, create these files in order:

1. **Request/Response Model** -- `features/{entity}/models/{Verb}{Entity}Model.cs`
2. **DTO** (if not exists) -- `dtos/{Entity}Dto.cs`
3. **Endpoint** -- `features/{entity}/endpoint/{Verb}{Entity}Endpoint.cs`
4. **AutoMapper Profile** (if not exists) -- `mappingprofiles/{Entity}MappingProfile.cs`

### Step 3: Validate with Checklist

Use the operation-specific checklists at the end of this file to verify the generated code before finishing.

### Step 4: Create Tests

Every endpoint requires tests. See `references/endpoint-testing.md` for the full testing guide, including EndpointTestBase, mapping profile tests, and naming conventions. For comprehensive cross-layer testing guidance, use the `create-tests` skill.

---

## References

For complete patterns, additional examples, and edge cases, consult files in `references/`:

| Topic | Reference |
|-------|-----------|
| Request/Response Models & DTOs | `references/request-response-models.md` |
| AutoMapper Profiles | `references/automapper-profiles.md` |
| Error Handling & BaseEndpoint | `references/error-handling.md` |
| Create Endpoint (POST) | `references/create-endpoint.md` |
| Get Endpoint (GET single) | `references/get-endpoint.md` |
| GetManyAndCount Endpoint (GET list) | `references/get-many-endpoint.md` |
| Update Endpoint (PUT) | `references/update-endpoint.md` |
| Delete Endpoint (DELETE) | `references/delete-endpoint.md` |
| Endpoint Testing | `references/endpoint-testing.md` |

---

## WebApi Layer Structure

```
src/{project}.webapi/
├── features/
│   ├── {entity}/
│   │   ├── endpoint/
│   │   │   ├── Create{Entity}Endpoint.cs
│   │   │   ├── Get{Entity}Endpoint.cs
│   │   │   ├── GetManyAndCount{Entities}Endpoint.cs
│   │   │   ├── Update{Entity}Endpoint.cs
│   │   │   └── Delete{Entity}Endpoint.cs
│   │   └── models/
│   │       ├── Create{Entity}Model.cs
│   │       ├── Get{Entity}Model.cs
│   │       ├── GetManyAndCount{Entities}Model.cs
│   │       ├── Update{Entity}Model.cs
│   │       └── Delete{Entity}Model.cs
│   └── BaseEndpoint.cs
├── dtos/
│   ├── {Entity}Dto.cs
│   └── GetManyAndCountResultDto.cs
└── mappingprofiles/
    ├── MappingProfile.cs                  <- Generic (one per project)
    └── {Entity}MappingProfile.cs          <- Per entity
```

---

## Endpoint Anatomy

Every endpoint class has two parts: `Configure()` and `HandleAsync()`. The `HandleAsync()` flow is always:

1. Map Request -> Command (AutoMapper)
2. Execute UseCase
3. Handle errors (`result.IsFailed` -> switch on exception type)
4. Map Entity -> Response (AutoMapper)
5. Send HTTP response

Always inject `AutoMapper.IMapper` (fully qualified) via primary constructor with `private readonly` field.
Always inherit from `BaseEndpoint<TRequest, TResponse>` for error handling helpers (except Delete which uses `Endpoint<TRequest>`).

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Endpoint file | `{Verb}{Entity}Endpoint.cs` | `CreateOrderEndpoint.cs` |
| Endpoint class | `{Verb}{Entity}Endpoint` | `CreateOrderEndpoint` |
| Model file | `{Verb}{Entity}Model.cs` | `CreateOrderModel.cs` |
| DTO file | `{Entity}Dto.cs` | `OrderDto.cs` |
| Profile file | `{Entity}MappingProfile.cs` | `OrderMappingProfile.cs` |
| Endpoint namespace | `{project}.webapi.features.{feature}.endpoint` | `app.webapi.features.orders.endpoint` |
| Model namespace | `{project}.webapi.features.{feature}.models` | `app.webapi.features.orders.models` |
| DTO namespace | `{project}.webapi.dtos` | `app.webapi.dtos` |
| Profile namespace | `{project}.webapi.mappingprofiles` | `app.webapi.mappingprofiles` |

---

## Endpoint Patterns Quick Reference

| Operation | Verb | Route | Success | Errors | Error Pattern | Key Notes | Reference |
|-----------|------|-------|---------|--------|---------------|-----------|-----------|
| CREATE | `Post` | `/{entities}` | 201 Created | 400, 409, 500 | `ExceptionalError` switch | `DontThrowIfValidationFails()`, `Send.CreatedAtAsync()` | `references/create-endpoint.md` |
| GET | `Get` | `/{entities}/{Id}` | 200 OK | 404, 500 | Custom error type switch | No transaction needed, route param auto-maps to Request | `references/get-endpoint.md` |
| GET MANY | `Get` | `/{entities}` | 200 OK | 500 | try-catch only | Query string passthrough via `HttpContext.Request.QueryString.Value` | `references/get-many-endpoint.md` |
| UPDATE | `Put` | `/{entities}/{Id}` | 200 OK | 400, 404, 409, 500 | `ExceptionalError` switch + try-catch | Route `{Id}` + body properties both map to Command | `references/update-endpoint.md` |
| DELETE | `Delete` | `/{entities}/{Id}` | 204 No Content | 404, 500 | `ExceptionalError` switch | No response body, uses `Endpoint<TRequest>` pattern | `references/delete-endpoint.md` |

Consult the reference file for the complete template, HandleAsync flow, and real-world example for each operation type.

---

## Exception-to-HTTP Mapping

| Domain Exception | HTTP Status | Status Code |
|-----------------|-------------|------------|
| `InvalidDomainException` | 400 Bad Request | `HttpStatusCode.BadRequest` |
| `DuplicatedDomainException` | 409 Conflict | `HttpStatusCode.Conflict` |
| `ResourceNotFoundException` | 404 Not Found | `HttpStatusCode.NotFound` |
| `ArgumentException` | 409 Conflict | `HttpStatusCode.Conflict` |
| `HttpRequestException` | 500 Internal Server Error | `HttpStatusCode.InternalServerError` |
| Generic `Exception` | 500 Internal Server Error | `HttpStatusCode.InternalServerError` |

---

## Checklists

### New Endpoint

- [ ] Inherits `BaseEndpoint<TRequest, TResponse>` (or `Endpoint<TRequest>` for Delete)
- [ ] Primary constructor injects `AutoMapper.IMapper` with `private readonly` field
- [ ] `Configure()` sets HTTP verb, route, `Description`, and `Policies`
- [ ] `DontThrowIfValidationFails()` included for POST/PUT endpoints
- [ ] `HandleAsync()` maps Request -> Command via AutoMapper
- [ ] `HandleAsync()` executes use case and checks `result.IsFailed`
- [ ] Error switch maps domain exceptions to correct HTTP status codes
- [ ] Success path maps Entity -> Response via AutoMapper and sends correct status
- [ ] CancellationToken passed to all async methods
- [ ] One file per endpoint, in `features/{entity}/endpoint/`

### New Request/Response Model

- [ ] Outer class follows `{Verb}{Entity}Model` naming
- [ ] Contains nested `Request` and `Response` classes
- [ ] XML doc comments on outer class, Request, Response, and all properties
- [ ] Strings initialized with `= string.Empty`
- [ ] Collections initialized with `= Enumerable.Empty<T>()`
- [ ] DTO references initialized with `= new()`
- [ ] Response wraps a `{Entity}Dto` (not domain entity)
- [ ] GetManyAndCount Response inherits `GetManyAndCountResultDto<{Entity}Dto>`
- [ ] File in `features/{entity}/models/`

### New DTO

- [ ] Named `{Entity}Dto` in `{project}.webapi.dtos` namespace
- [ ] Only properties, no methods or logic
- [ ] XML doc comments on class and all properties
- [ ] Strings initialized with `= string.Empty`
- [ ] Uses `IEnumerable<T>` for collections (not `List<T>`)
- [ ] Does not expose domain entity properties (e.g., no `UserId`, no `PasswordHash`)
- [ ] File in `dtos/`

### New AutoMapper Profile

- [ ] Named `{Entity}MappingProfile` inheriting `AutoMapper.Profile`
- [ ] Contains Entity -> DTO mapping
- [ ] Contains DAO -> DTO mapping (for GetManyAndCount)
- [ ] Contains Request -> Command mappings (one per operation)
- [ ] Contains Entity -> Response mappings with `ForMember` for DTO nesting
- [ ] Contains DAO paginated -> DTO paginated mapping (for GetManyAndCount)
- [ ] XML doc comment on class
- [ ] File in `mappingprofiles/`

---

## Examples

### Example 1: Create a POST endpoint for creating orders

User says: "Create a POST endpoint for creating orders"

Actions:
1. Create `CreateOrderModel.cs` in `features/orders/models/` with nested `Request` (properties: `OrderNumber`, `Total`, `Status`) and `Response` (wraps `OrderDto`)
2. Create `OrderDto.cs` in `dtos/` with properties matching API contract
3. Create `CreateOrderEndpoint.cs` in `features/orders/endpoint/` inheriting `BaseEndpoint`, with `Post("/orders")`, AutoMapper mapping, error switch (InvalidDomain->400, Duplicated->409), and `Send.CreatedAtAsync` for 201
4. Create `OrderMappingProfile.cs` in `mappingprofiles/` with `CreateMap<Order, OrderDto>()`, `CreateMap<CreateOrderModel.Request, CreateOrderUseCase.Command>()`, and `CreateMap<Order, CreateOrderModel.Response>().ForMember(...)`
5. Create endpoint tests and mapping profile tests

Result: `CreateOrderEndpoint.cs`, `CreateOrderModel.cs`, `OrderDto.cs`, and `OrderMappingProfile.cs` created. Endpoint handles 201/400/409/500 responses.

### Example 2: Add a GET endpoint to list products with pagination

User says: "Add a GET endpoint to list products with pagination"

Actions:
1. Create `GetManyAndCountProductsModel.cs` with empty `Request` and `Response` inheriting `GetManyAndCountResultDto<ProductDto>`
2. Create `ProductDto.cs` in `dtos/` if not exists
3. Create `GetManyAndCountProductsEndpoint.cs` with `Get("/products")`, `HttpContext.Request.QueryString.Value` passthrough, try-catch error handling, and `Send.OkAsync` for 200
4. Add to `ProductMappingProfile.cs`: `CreateMap<ProductDao, ProductDto>()` and `CreateMap<GetManyAndCountResultDto<ProductDao>, GetManyAndCountResultDto<ProductDto>>()`
5. Verify generic `MappingProfile.cs` exists with `GetManyAndCountResult<>` -> `GetManyAndCountResultDto<>` mapping
6. Create tests

Result: `GetManyAndCountProductsEndpoint.cs` created using query string passthrough pattern. Supports pagination, sorting, and filtering via query parameters.

### Example 3: Generate all CRUD endpoints for TechnicalStandard

User says: "Generate all CRUD endpoints for TechnicalStandard"

Actions:
1. Create DTOs: `TechnicalStandardDto.cs`
2. Create Models: `CreateTechnicalStandardModel.cs`, `GetTechnicalStandardModel.cs`, `GetManyAndCountTechnicalStandardsModel.cs`, `UpdateTechnicalStandardModel.cs`, `DeleteTechnicalStandardModel.cs`
3. Create Endpoints: `CreateTechnicalStandardEndpoint.cs` (POST, 201), `GetTechnicalStandardEndpoint.cs` (GET, 200), `GetManyAndCountTechnicalStandardsEndpoint.cs` (GET, 200), `UpdateTechnicalStandardEndpoint.cs` (PUT, 200), `DeleteTechnicalStandardEndpoint.cs` (DELETE, 204)
4. Create `TechnicalStandardMappingProfile.cs` with all required mappings
5. Create tests for all endpoints and mapping profile

Result: Five endpoint files, five model files, one DTO, and one mapping profile created. Full CRUD coverage with appropriate error handling per operation.

---

## Troubleshooting

### Endpoint not discovered by FastEndpoints
Cause: Endpoint class is not `public`, or it does not inherit from `Endpoint<TRequest, TResponse>` / `BaseEndpoint<TRequest, TResponse>`.
Solution: Ensure the endpoint class is `public` and inherits from the correct base class. FastEndpoints auto-discovers all public `Endpoint<>` subclasses in the assembly.

### AutoMapper mapping not found
Cause: No `CreateMap<TSource, TDestination>()` configured for the source-destination pair, or the profile is not in the scanned assembly.
Solution: Add the missing mapping to the entity's `MappingProfile`. Verify `builder.Services.AddAutoMapper(typeof(Program).Assembly)` is in `Program.cs`.

### Route parameter not binding to Request property
Cause: The route parameter name `{Id}` does not match the Request property name `Id` (case-insensitive).
Solution: Ensure the route parameter name matches the Request property name. FastEndpoints is case-insensitive but the property must exist.

### 500 error instead of expected 400/404/409
Cause: The error switch does not match the exception type, falling through to `default` which returns 500.
Solution: Verify the use case wraps exceptions correctly with `Result.Fail(new Error(message).CausedBy(exception))`. Check that `ExceptionalError` extraction pattern matches: `error?.Reasons.OfType<ExceptionalError>().FirstOrDefault()?.Exception`.

### GetManyAndCount returns empty results
Cause: Missing mapping from `GetManyAndCountResult<>` to `GetManyAndCountResultDto<>` in the generic `MappingProfile.cs`, or missing DAO -> DTO mapping.
Solution: Verify `MappingProfile.cs` contains the generic `CreateMap(typeof(GetManyAndCountResult<>), typeof(GetManyAndCountResultDto<>))` mapping. Verify the entity profile has `CreateMap<{Entity}Dao, {Entity}Dto>()`.

### Response wraps entity instead of DTO
Cause: Missing Entity -> Response `ForMember` mapping, causing AutoMapper to try direct assignment.
Solution: Add `CreateMap<Entity, CreateEntityModel.Response>().ForMember(dest => dest.Entity, opt => opt.MapFrom(src => src))` to the mapping profile. This chains through the Entity -> DTO mapping.

### BaseEndpoint helpers not available
Cause: Endpoint inherits from `Endpoint<TRequest, TResponse>` instead of `BaseEndpoint<TRequest, TResponse>`.
Solution: Change inheritance to `BaseEndpoint<TRequest, TResponse>` to get access to `HandleErrorAsync`, `HandleUnexpectedErrorAsync`, and `HandleErrorWithMessageAsync`.

---

## Related

- **Application Layer:** `create-use-case` -- Use cases, Command/Handler pattern, transaction management
- **Domain Layer:** `create-domain` -- Entities, validators, value objects, domain exceptions
- **Infrastructure Layer:** `create-repository` -- Repository implementations, mappers, Unit of Work
- **Testing:** See `references/endpoint-testing.md` -- Unit tests, integration tests, conventions
