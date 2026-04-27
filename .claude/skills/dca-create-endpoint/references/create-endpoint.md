# Create Endpoint Reference

## Pattern Summary

- **HTTP Verb**: `Post("/entity")`
- **Base Class**: `BaseEndpoint<Create{Entity}Model.Request, Create{Entity}Model.Response>`
- **Success Response**: 201 Created with `Send.CreatedAtAsync()`
- **Error Handling**: `InvalidDomainException` (400), `DuplicatedDomainException` (409), Generic (500)

## Complete Template

```csharp
using FastEndpoints;
using FluentResults;
using {project}.domain.exceptions;
using {project}.webapi.dtos;
using {project}.webapi.features.{feature}.models;
using {project}.application.usecases.{feature};

namespace {project}.webapi.features.{feature}.endpoint;

/// <summary>
/// Endpoint for creating a new {entity}.
/// </summary>
public class Create{Entity}Endpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<Create{Entity}Model.Request, Create{Entity}Model.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Post("/{entities}");
        Description(b => b
            .Produces<{Entity}Dto>(StatusCodes.Status201Created)
            .ProducesProblemDetails(StatusCodes.Status400BadRequest)
            .ProducesProblemDetails(StatusCodes.Status409Conflict)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        DontThrowIfValidationFails();
        Policies("MustBeApplicationUser");
    }

    public override async Task HandleAsync(
        Create{Entity}Model.Request request,
        CancellationToken ct)
    {
        // 1. Map Request -> Command
        var command = _mapper.Map<Create{Entity}UseCase.Command>(request);

        // 2. Execute use case
        var result = await command.ExecuteAsync(ct);

        // 3. Handle errors
        if (result.IsFailed)
        {
            var error = result.Errors.FirstOrDefault();
            var errorType = error?.Reasons.OfType<ExceptionalError>().FirstOrDefault();

            switch (errorType?.Exception)
            {
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
            return;
        }

        // 4. Map Entity -> Response
        var response = _mapper.Map<Create{Entity}Model.Response>(result.Value);

        // 5. Send 201 Created
        await Send.CreatedAtAsync(
            $"/{entities}/{response.{Entity}.Id}",
            new[] { response.{Entity}.Id },
            response,
            false,
            ct);
    }
}
```

## HandleAsync Flow

```
Request -> Map to Command -> Execute UseCase -> Check IsFailed
                                                    |
                                              +-----+-----+
                                              |           |
                                           Failed      Success
                                              |           |
                                     Switch on Exception  Map to Response
                                     InvalidDomain->400   Send.CreatedAtAsync (201)
                                     Duplicated->409
                                     Default->500
```

## Key Points

1. **`DontThrowIfValidationFails()`** -- Always include to handle validation manually
2. **`Send.CreatedAtAsync()`** -- Use for 201 with Location header, pass `false` for `generateAbsoluteUrl`
3. **AutoMapper** -- Map request to command, then entity to response. Never construct commands manually
4. **No try-catch** -- The FluentResults `result.IsFailed` pattern replaces try-catch at endpoint level
5. **Error switch** -- Use `ExceptionalError` pattern for domain exceptions wrapped in FluentResults

## Real Example: CreateUserEndpoint

```csharp
public class CreateUserEndpoint(AutoMapper.IMapper mapper)
    : Endpoint<CreateUserModel.Request, CreateUserModel.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;

    public override void Configure()
    {
        Post("/users");
        Description(b => b
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesProblemDetails(StatusCodes.Status400BadRequest)
            .ProducesProblemDetails(StatusCodes.Status409Conflict)
            .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
        DontThrowIfValidationFails();
        Policies("MustBeApplicationAdministrator");
    }

    public override async Task HandleAsync(CreateUserModel.Request request, CancellationToken ct)
    {
        var command = _mapper.Map<CreateUserUseCase.Command>(request);
        var result = await command.ExecuteAsync(ct);

        if (result.IsFailed)
        {
            var error = result.Errors.FirstOrDefault();

            if (error?.Reasons.OfType<ExceptionalError>()
                .Any(r => r.Exception is InvalidDomainException) == true)
            {
                AddError(error.Message);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }

            if (error?.Reasons.OfType<ExceptionalError>()
                .Any(r => r.Exception is DuplicatedDomainException) == true)
            {
                AddError(error.Message);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
            }

            AddError(error?.Message ?? "Unknown error");
            await Send.ErrorsAsync(StatusCodes.Status500InternalServerError, ct);
            return;
        }

        var userResponse = _mapper.Map<CreateUserModel.Response>(result.Value);
        await Send.CreatedAtAsync(
            $"/users/{userResponse.User.Id}",
            new[] { userResponse.User.Id },
            userResponse,
            false,
            ct);
    }
}
```
