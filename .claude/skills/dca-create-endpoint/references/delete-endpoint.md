# Delete Endpoint Reference

## Pattern Summary

- **HTTP Verb**: `Delete("/entity/{Id}")`
- **Base Class**: `Endpoint<Delete{Entity}Model.Request>` (no TResponse)
- **Success Response**: 204 No Content with `Send.NoContentAsync()`
- **Error Handling**: `ResourceNotFoundException` (404), Generic (500)

## Complete Template

```csharp
using FastEndpoints;
using FluentResults;
using System.Net;
using {project}.domain.exceptions;
using {project}.webapi.features.{feature}.models;
using {project}.application.usecases.{feature};

namespace {project}.webapi.features.{feature}.endpoint;

/// <summary>
/// Endpoint for deleting a {entity}.
/// </summary>
public class Delete{Entity}Endpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<Delete{Entity}Model.Request, object>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Delete("/{entities}/{Id}");
        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemDetails(StatusCodes.Status404NotFound)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        Policies("MustBeApplicationUser");
    }

    public override async Task HandleAsync(
        Delete{Entity}Model.Request req,
        CancellationToken ct)
    {
        // 1. Execute use case directly (Id is the only input)
        var command = new Delete{Entity}UseCase.Command { Id = req.Id };
        var result = await command.ExecuteAsync(ct);

        // 2. Handle errors
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
                        error, error?.Message ?? "Unknown error", ct,
                        HttpStatusCode.InternalServerError);
                    break;
            }
            return;
        }

        // 3. Send 204 No Content
        await Send.NoContentAsync(ct);
    }
}
```

## HandleAsync Flow

```
Request -> Create Command (Id) -> Execute UseCase -> Check IsFailed
                                                         |
                                                   +-----+-----+
                                                   |           |
                                                Failed      Success
                                                   |           |
                                          Switch on Exception  Send.NoContentAsync (204)
                                          NotFound->404
                                          Default->500
```

## Key Differences from Other Endpoints

| Aspect | Delete | Other Endpoints |
|--------|--------|----------------|
| Response type | No TResponse (or `object`) | `{Verb}{Entity}Model.Response` |
| Success code | 204 No Content | 200 OK or 201 Created |
| AutoMapper needed | Only for complex requests | Yes (Request -> Command, Entity -> Response) |
| Use case result | `Result` (void, no entity) | `Result<Entity>` |
| Model file | Request only, no Response class | Both Request and Response |

## Key Points

1. **`Send.NoContentAsync()`** -- Delete returns no body, only 204 status
2. **No AutoMapper for response** -- No entity to map back since delete returns void
3. **Simple command** -- Usually only `Id` property, create directly without AutoMapper
4. **Two exceptions only** -- NotFound (404) and generic (500). No validation or duplicate errors
5. **`Endpoint<TRequest>` or `BaseEndpoint<TRequest, object>`** -- Use `object` as TResponse to still get BaseEndpoint helpers

## Alternative: Using Endpoint<TRequest> Directly

If `BaseEndpoint` helpers are not needed:

```csharp
public class Delete{Entity}Endpoint
    : Endpoint<Delete{Entity}Model.Request>
{
    public override void Configure()
    {
        Delete("/{entities}/{Id}");
        Policies("MustBeApplicationUser");
    }

    public override async Task HandleAsync(
        Delete{Entity}Model.Request req, CancellationToken ct)
    {
        var command = new Delete{Entity}UseCase.Command { Id = req.Id };
        var result = await command.ExecuteAsync(ct);

        if (result.IsFailed)
        {
            AddError(result.Errors.First().Message);
            await Send.ErrorsAsync(StatusCodes.Status404NotFound, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
```
