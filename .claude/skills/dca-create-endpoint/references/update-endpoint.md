# Update Endpoint Reference

## Pattern Summary

- **HTTP Verb**: `Put("/entity/{Id}")`
- **Base Class**: `BaseEndpoint<Update{Entity}Model.Request, Update{Entity}Model.Response>`
- **Success Response**: 200 OK with `Send.OkAsync()`
- **Error Handling**: `ResourceNotFoundException` (404), `InvalidDomainException` (400), `DuplicatedDomainException` (409), Generic (500)
- **Route + Body**: `Id` from route, other properties from JSON body

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
/// Endpoint for updating an existing {entity}.
/// </summary>
public class Update{Entity}Endpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<Update{Entity}Model.Request, Update{Entity}Model.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Put("/{entities}/{Id}");
        Description(b => b
            .Produces<Update{Entity}Model.Response>(StatusCodes.Status200OK)
            .ProducesProblemDetails(StatusCodes.Status400BadRequest)
            .ProducesProblemDetails(StatusCodes.Status404NotFound)
            .ProducesProblemDetails(StatusCodes.Status409Conflict)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        Policies("MustBeApplicationUser");
        Summary(s =>
        {
            s.Summary = "Update an existing {entity}";
            s.Response(200, "{Entity} updated");
            s.Response(404, "{Entity} not found");
            s.Response(400, "Invalid data");
            s.Response(409, "Duplicate detected");
        });
    }

    public override async Task HandleAsync(
        Update{Entity}Model.Request req,
        CancellationToken ct)
    {
        try
        {
            // 1. Map Request -> Command (includes Id from route)
            var command = _mapper.Map<Update{Entity}UseCase.Command>(req);

            // 2. Execute use case
            var result = await command.ExecuteAsync(ct);

            // 3. Success path
            if (result.IsSuccess)
            {
                var response = _mapper.Map<Update{Entity}Model.Response>(result.Value);
                await Send.OkAsync(response, ct);
                return;
            }

            // 4. Error path -- switch on exception type
            var error = result.Errors.FirstOrDefault();
            var errorType = error?.Reasons.OfType<ExceptionalError>().FirstOrDefault();

            switch (errorType?.Exception)
            {
                case ResourceNotFoundException:
                    await HandleErrorWithMessageAsync(
                        error, error?.Message ?? "", ct, HttpStatusCode.NotFound);
                    break;
                case InvalidDomainException:
                    await HandleErrorWithMessageAsync(
                        error, error?.Message ?? "", ct, HttpStatusCode.BadRequest);
                    break;
                case DuplicatedDomainException:
                    await HandleErrorWithMessageAsync(
                        error, error?.Message ?? "", ct, HttpStatusCode.Conflict);
                    break;
                default:
                    await HandleErrorWithMessageAsync(
                        error, error?.Message ?? "Unknown error", ct, HttpStatusCode.InternalServerError);
                    break;
            }
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status500InternalServerError, ct);
        }
    }
}
```

## HandleAsync Flow

```
Request -> Map to Command -> Execute UseCase -> Check IsSuccess
                                                    |
                                              +-----+-----+
                                              |           |
                                           Failed      Success
                                              |           |
                                   Switch on Exception  Map to Response
                                   NotFound->404        Send.OkAsync (200)
                                   InvalidDomain->400
                                   Duplicated->409
                                   Default->500
                                              |
                                   Outer catch->500
```

## Key Points

1. **3-exception switch** -- Update can fail with NotFound (entity doesn't exist), InvalidDomain (validation), or Duplicated (unique constraint)
2. **Route + Body** -- `Id` is bound from route parameter `{Id}`, other properties from JSON body. AutoMapper maps all to Command
3. **try-catch wrapper** -- Update endpoints wrap with an outer try-catch as safety net
4. **`result.IsSuccess` first** -- Check success path first, then handle errors (cleaner flow)
5. **`Send.OkAsync()`** -- Returns 200 OK with updated entity DTO

## Request Binding Example

```http
PUT /technical-standards/550e8400-e29b-41d4-a716-446655440000 HTTP/1.1
Content-Type: application/json

{
  "code": "ISO-9001",
  "name": "Quality Management System",
  "edition": "2024"
}
```

FastEndpoints binds:
- `Id` = `550e8400-e29b-41d4-a716-446655440000` (from route)
- `Code` = `"ISO-9001"` (from body)
- `Name` = `"Quality Management System"` (from body)
- `Edition` = `"2024"` (from body)

## Real Example: UpdateTechnicalStandardEndpoint

```csharp
public class UpdateTechnicalStandardEndpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<UpdateTechnicalStandardModel.Request, UpdateTechnicalStandardModel.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Put("/technical-standards/{Id}");
        Policies("MustBeApplicationUser");
        Summary(s =>
        {
            s.Summary = "Update an existing technical standard";
            s.Response(200, "Technical standard updated");
            s.Response(404, "Technical standard not found");
            s.Response(400, "Invalid data");
            s.Response(409, "Duplicate code");
        });
    }

    public override async Task HandleAsync(
        UpdateTechnicalStandardModel.Request req, CancellationToken ct)
    {
        try
        {
            var command = _mapper.Map<UpdateTechnicalStandardUseCase.Command>(req);
            var result = await command.ExecuteAsync(ct);

            if (result.IsSuccess)
            {
                var response = _mapper.Map<UpdateTechnicalStandardModel.Response>(result.Value);
                await Send.OkAsync(response, ct);
                return;
            }

            var error = result.Errors.FirstOrDefault();
            var errorType = error?.Reasons.OfType<ExceptionalError>().FirstOrDefault();

            switch (errorType?.Exception)
            {
                case InvalidDomainException:
                    await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.BadRequest);
                    break;
                case DuplicatedDomainException:
                    await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.Conflict);
                    break;
                case ResourceNotFoundException:
                    await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.NotFound);
                    break;
                default:
                    await HandleErrorWithMessageAsync(error, error?.Message ?? "Unknown error", ct, HttpStatusCode.InternalServerError);
                    break;
            }
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status500InternalServerError, ct);
        }
    }
}
```
