# GetManyAndCount Endpoint Reference

## Pattern Summary

- **HTTP Verb**: `Get("/entities")`
- **Base Class**: `Endpoint<GetManyAndCount{Entities}Model.Request, GetManyAndCountResultDto<{Entity}Dto>>`
- **Success Response**: 200 OK with `Send.OkAsync()` containing paginated data
- **Error Handling**: try-catch with Generic Exception (500) only
- **Query String**: Passed directly via `HttpContext.Request.QueryString.Value`

## Complete Template

```csharp
using FastEndpoints;
using {project}.webapi.dtos;
using {project}.webapi.features.{feature}.models;
using {project}.application.usecases.{feature};

namespace {project}.webapi.features.{feature}.endpoint;

/// <summary>
/// Endpoint for retrieving a paginated list of {entities}.
/// </summary>
public class GetManyAndCount{Entities}Endpoint(AutoMapper.IMapper mapper)
    : Endpoint<GetManyAndCount{Entities}Model.Request, GetManyAndCountResultDto<{Entity}Dto>>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Get("/{entities}");
        Description(d => d
            .WithTags("{Entities}")
            .Produces<GetManyAndCountResultDto<{Entity}Dto>>(StatusCodes.Status200OK)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        DontThrowIfValidationFails();
        Policies("MustBeApplicationUser");
    }

    public override async Task HandleAsync(
        GetManyAndCount{Entities}Model.Request req,
        CancellationToken ct)
    {
        try
        {
            // 1. Create command with full query string
            var command = new GetManyAndCount{Entities}UseCase.Command
            {
                Query = HttpContext.Request.QueryString.Value
            };

            // 2. Execute use case
            var getManyAndCountResult = await command.ExecuteAsync(ct);

            // 3. Map domain result -> DTO
            var response = _mapper.Map<GetManyAndCountResultDto<{Entity}Dto>>(
                getManyAndCountResult);

            // 4. Send 200 OK
            Logger.LogInformation("Successfully retrieved {entities}");
            await Send.OkAsync(response, cancellation: ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve {entities}");
            AddError(ex.Message);
            await Send.ErrorsAsync(
                StatusCodes.Status500InternalServerError, cancellation: ct);
        }
    }
}
```

## HandleAsync Flow

```
Request -> Create Command (with QueryString) -> Execute UseCase
                                                      |
                                                 try-catch
                                                   |     |
                                               Success  Exception
                                                   |     |
                                          Map result  Log + 500
                                          Send.OkAsync (200)
```

## Key Differences from Other Endpoints

| Aspect | GetManyAndCount | Other Endpoints |
|--------|----------------|-----------------|
| Error handling | try-catch (no FluentResults) | `result.IsFailed` pattern |
| Response type | `GetManyAndCountResultDto<TDto>` | `{Verb}{Entity}Model.Response` |
| Query params | `HttpContext.Request.QueryString.Value` | Route params / body |
| Command creation | Direct (no AutoMapper) | AutoMapper |
| Domain result | `GetManyAndCountResult<T>` (not wrapped in `Result<>`) | `Result<Entity>` |

## Query String Format

The query string supports pagination, sorting, and filtering:

```
?pageNumber=1&pageSize=25&sortBy=Name&sortDirection=asc&Status=Active||eq&Name=John||contains
```

The `QueryStringParser` in Infrastructure layer parses this into NHibernate queries.

## Mapping Chain

```
GetManyAndCountResult<EntityDao>       (domain)
    ↓ MappingProfile (generic)
GetManyAndCountResultDto<EntityDao>    (DTO with DAO items)
    ↓ EntityMappingProfile
GetManyAndCountResultDto<EntityDto>    (DTO with DTO items)
    ↓ JSON serialization
HTTP 200 OK Response
```

## Response JSON Example

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "code": "ISO-9001",
      "name": "Quality Management System"
    },
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "code": "ISO-14001",
      "name": "Environmental Management"
    }
  ],
  "count": 42,
  "pageNumber": 1,
  "pageSize": 25,
  "sortBy": "Name",
  "sortCriteria": "asc"
}
```

## Real Example: GetManyAndCountUsersEndpoint

```csharp
public class GetManyAndCountUsersEndPoint(AutoMapper.IMapper mapper)
    : Endpoint<GetManyAndCountModel.Request, GetManyAndCountResultDto<UserDto>>
{
    private readonly AutoMapper.IMapper mapper = mapper;

    public override void Configure()
    {
        Get("/users");
        Description(d => d
            .WithTags("Users")
            .WithName("GetManyAndCountUsers")
            .Produces<GetManyAndCountResultDto<UserDto>>(200));
        DontThrowIfValidationFails();
        Policies("MustBeApplicationUser");
    }

    override public async Task HandleAsync(
        GetManyAndCountModel.Request req, CancellationToken ct)
    {
        try
        {
            var request = new GetManyAndCountUsersUseCase.Command
            {
                Query = HttpContext.Request.QueryString.Value
            };

            var getManyAndCountResult = await request.ExecuteAsync(ct);
            var response = mapper.Map<GetManyAndCountResultDto<UserDto>>(
                getManyAndCountResult);

            Logger.LogInformation("Successfully retrieved users");
            await Send.OkAsync(response, cancellation: ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve users");
            AddError(ex.Message);
            await Send.ErrorsAsync(
                StatusCodes.Status500InternalServerError, cancellation: ct);
        }
    }
}
```
