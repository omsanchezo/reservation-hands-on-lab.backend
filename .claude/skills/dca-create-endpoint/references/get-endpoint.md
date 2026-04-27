# Get Endpoint Reference

## Pattern Summary

- **HTTP Verb**: `Get("/entity/{Id}")` or `Get("/entity/{UniqueField}")`
- **Base Class**: `BaseEndpoint<Get{Entity}Model.Request, Get{Entity}Model.Response>`
- **Success Response**: 200 OK with `Send.OkAsync()`
- **Error Handling**: Custom error type or `ResourceNotFoundException` (404), Generic (500)

## Complete Template

```csharp
using FastEndpoints;
using FluentResults;
using System.Net;
using {project}.webapi.features.{feature}.models;
using {project}.application.usecases.{feature};

namespace {project}.webapi.features.{feature}.endpoint;

/// <summary>
/// Endpoint for retrieving a {entity} by {identifier}.
/// </summary>
public class Get{Entity}Endpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<Get{Entity}Model.Request, Get{Entity}Model.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Get("/{entities}/{Id}");
        Description(d => d
            .WithTags("{Entities}")
            .Produces<Get{Entity}Model.Response>(StatusCodes.Status200OK)
            .ProducesProblemDetails(StatusCodes.Status404NotFound)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        Policies("MustBeApplicationUser");
    }

    public override async Task HandleAsync(
        Get{Entity}Model.Request req,
        CancellationToken ct)
    {
        // 1. Create command (direct assignment, no mapper needed for simple Get)
        var command = new Get{Entity}UseCase.Command { Id = req.Id };

        // 2. Execute use case
        var result = await command.ExecuteAsync(ct);

        // 3. Handle errors
        if (result.IsFailed)
        {
            var error = result.Errors[0];
            switch (error)
            {
                case {Entity}NotFoundError e:
                    await HandleErrorAsync(
                        r => r.Id, e.Message, HttpStatusCode.NotFound, ct);
                    break;
                default:
                    await HandleUnexpectedErrorAsync(error, ct);
                    break;
            }
            return;
        }

        // 4. Map Entity -> Response and send 200 OK
        var response = _mapper.Map<Get{Entity}Model.Response>(result.Value);
        await Send.OkAsync(response, ct);
    }
}
```

## HandleAsync Flow

```
Request -> Create Command -> Execute UseCase -> Check IsFailed
                                                    |
                                              +-----+-----+
                                              |           |
                                           Failed      Success
                                              |           |
                                     Switch on error type Map to Response
                                     NotFound->404        Send.OkAsync (200)
                                     Default->500
```

## Key Points

1. **No transaction** -- Get (single entity) is read-only, no `BeginTransaction` needed
2. **Route parameter** -- `{Id}` in route maps to `Request.Id` automatically
3. **Custom error types** -- Use pattern matching on custom `Error` subtypes (e.g., `UserNotFoundError`)
4. **Simple command** -- For Get endpoints, command is often created directly (no mapper needed for 1-2 properties)
5. **`Send.OkAsync()`** -- Returns 200 OK with the response body

## Alternative: Get by Unique Field

```csharp
public override void Configure()
{
    Get("/users/{UserName}");
    // ...
}

public override async Task HandleAsync(GetUserModel.Request req, CancellationToken ct)
{
    var command = new GetUserUseCase.Command { UserName = req.UserName };
    var result = await command.ExecuteAsync(ct);

    if (result.IsFailed)
    {
        var error = result.Errors[0];
        switch (error)
        {
            case UserNotFoundError e:
                await HandleErrorAsync(r => r.UserName, e.Message, HttpStatusCode.NotFound, ct);
                break;
            default:
                await HandleUnexpectedErrorAsync(error, ct);
                break;
        }
        return;
    }

    var response = _mapper.Map<GetUserModel.Response>(result.Value);
    await Send.OkAsync(response, ct);
}
```

## Alternative: ExceptionalError Pattern

When the use case wraps exceptions in FluentResults instead of custom error types:

```csharp
if (result.IsFailed)
{
    var error = result.Errors.FirstOrDefault();
    var errorType = error?.Reasons.OfType<ExceptionalError>().FirstOrDefault();

    switch (errorType?.Exception)
    {
        case ResourceNotFoundException:
            await HandleErrorWithMessageAsync(
                error, error?.Message ?? "", ct, HttpStatusCode.NotFound);
            break;
        default:
            await HandleErrorWithMessageAsync(
                error, error?.Message ?? "Unknown error", ct, HttpStatusCode.InternalServerError);
            break;
    }
    return;
}
```
