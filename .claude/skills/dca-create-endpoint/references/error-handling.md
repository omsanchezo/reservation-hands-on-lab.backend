# Error Handling Reference

## BaseEndpoint Class

All endpoints inherit from `BaseEndpoint<TRequest, TResponse>` which provides consistent error handling helpers:

```csharp
using FastEndpoints;
using FluentResults;
using System.Linq.Expressions;
using System.Net;

namespace {project}.webapi.features;

/// <summary>
/// Base endpoint with helpers for error handling.
/// </summary>
public abstract class BaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    private const string UnexpectedErrorMessage = "An unexpected error occurred.";

    /// <summary>
    /// Returns the provided message or a default error message if the input is null or empty.
    /// </summary>
    protected string GetMessageOrDefault(string? message) =>
        string.IsNullOrEmpty(message) ? UnexpectedErrorMessage : message;

    /// <summary>
    /// Helper for property-based error handling.
    /// Logs warning, adds error to property, sends error response.
    /// </summary>
    protected async Task HandleErrorAsync(
        Expression<Func<TRequest, object?>> property,
        string message,
        HttpStatusCode status,
        CancellationToken ct)
    {
        Logger.LogWarning(message);
        AddError(property, message);
        await Send.ErrorsAsync(statusCode: (int)status, cancellation: ct);
    }

    /// <summary>
    /// Helper for unexpected error handling from FluentResults IError.
    /// </summary>
    protected async Task HandleUnexpectedErrorAsync(
        IError? error,
        CancellationToken ct,
        HttpStatusCode status = HttpStatusCode.InternalServerError)
    {
        var exception = ExtractExceptionFromError(error);
        await LogAndSendErrorAsync(exception, UnexpectedErrorMessage, status, ct);
    }

    /// <summary>
    /// Helper for error handling with a custom message from FluentResults IError.
    /// </summary>
    protected async Task HandleErrorWithMessageAsync(
        IError? error,
        string message,
        CancellationToken ct,
        HttpStatusCode status = HttpStatusCode.InternalServerError)
    {
        var errorMessage = GetMessageOrDefault(message);
        var exception = ExtractExceptionFromError(error);
        await LogAndSendErrorAsync(exception, errorMessage, status, ct);
    }

    /// <summary>
    /// Helper for unexpected error handling from raw Exception.
    /// </summary>
    protected async Task HandleUnexpectedErrorAsync(
        Exception? ex,
        CancellationToken ct,
        HttpStatusCode status = HttpStatusCode.InternalServerError)
    {
        await LogAndSendErrorAsync(ex, UnexpectedErrorMessage, status, ct);
    }

    private static Exception? ExtractExceptionFromError(IError? error)
    {
        if (error?.Metadata?.TryGetValue("Exception", out var exObj) == true && exObj is Exception ex)
            return ex;
        return null;
    }

    private async Task LogAndSendErrorAsync(
        Exception? ex, string message, HttpStatusCode status, CancellationToken ct)
    {
        if (ex != null)
            Logger.LogError(ex, message);
        else
            Logger.LogError(message);

        AddError(message);
        await Send.ErrorsAsync(statusCode: (int)status, cancellation: ct);
    }
}
```

## 4 Helper Methods

| Method | When to Use | Logs As | Default Status |
|--------|------------|---------|----------------|
| `HandleErrorAsync` | Known domain errors with a specific request property | Warning | (required) |
| `HandleUnexpectedErrorAsync(IError)` | Default/fallback for FluentResults errors | Error | 500 |
| `HandleErrorWithMessageAsync` | Domain exceptions wrapped in FluentResults with custom message | Error | 500 |
| `HandleUnexpectedErrorAsync(Exception)` | Raw exceptions in catch blocks | Error | 500 |

## Domain Exception to HTTP Status Mapping

| Domain Exception | HTTP Status | Code | Use Case |
|-----------------|-------------|------|----------|
| `InvalidDomainException` | 400 Bad Request | `HttpStatusCode.BadRequest` | Validation failed |
| `DuplicatedDomainException` | 409 Conflict | `HttpStatusCode.Conflict` | Duplicate record |
| `ResourceNotFoundException` | 404 Not Found | `HttpStatusCode.NotFound` | Entity not found |
| `ArgumentException` | 409 Conflict | `HttpStatusCode.Conflict` | Invalid argument |
| `HttpRequestException` | 500 Internal Server Error | `HttpStatusCode.InternalServerError` | External service error |
| Generic `Exception` | 500 Internal Server Error | `HttpStatusCode.InternalServerError` | Unexpected error |

## Error Handling Pattern: ExceptionalError Switch

The standard pattern for handling FluentResults errors that wrap domain exceptions:

```csharp
if (result.IsFailed)
{
    var error = result.Errors.FirstOrDefault();
    var errorType = error?.Reasons.OfType<ExceptionalError>().FirstOrDefault();

    switch (errorType?.Exception)
    {
        case ResourceNotFoundException:
            await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.NotFound);
            break;
        case InvalidDomainException:
            await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.BadRequest);
            break;
        case DuplicatedDomainException:
            await HandleErrorWithMessageAsync(error, error?.Message ?? "", ct, HttpStatusCode.Conflict);
            break;
        default:
            await HandleErrorWithMessageAsync(error, error?.Message ?? "Unknown error", ct, HttpStatusCode.InternalServerError);
            break;
    }
    return;
}
```

## Error Handling Pattern: Custom Error Types Switch

For endpoints using custom FluentResults error types (e.g., `UserNotFoundError`):

```csharp
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
```

## Which Exceptions Apply per CRUD Operation

| Operation | Possible Exceptions |
|-----------|-------------------|
| CREATE | `InvalidDomainException` (400), `DuplicatedDomainException` (409), Generic (500) |
| GET | `ResourceNotFoundException` or custom error (404), Generic (500) |
| GET MANY AND COUNT | Generic `Exception` only (500, via try-catch) |
| UPDATE | `ResourceNotFoundException` (404), `InvalidDomainException` (400), `DuplicatedDomainException` (409), Generic (500) |
| DELETE | `ResourceNotFoundException` (404), Generic (500) |

## Swagger Error Documentation

Document expected error codes in `Configure()`:

```csharp
public override void Configure()
{
    Post("/entity");
    Description(b => b
        .Produces<EntityDto>(StatusCodes.Status201Created)
        .ProducesProblemDetails(StatusCodes.Status400BadRequest)
        .ProducesProblemDetails(StatusCodes.Status409Conflict)
        .ProducesProblemDetails(StatusCodes.Status500InternalServerError));
}
```

## Anti-Patterns

```csharp
// NEVER: Expose internal exception details to client
catch (Exception ex)
{
    AddError(ex.ToString());  // Exposes stack trace!
}

// NEVER: Use wrong status codes
if (!validationResult.IsValid)
    await Send.ErrorsAsync(500, ct);  // Should be 400

// NEVER: Skip logging
if (result.IsFailed)
{
    AddError("Not found");
    await Send.ErrorsAsync(404, ct);  // No logging!
}

// NEVER: Inherit from Endpoint<> directly when BaseEndpoint is available
public class MyEndpoint : Endpoint<Req, Res> { }  // Use BaseEndpoint
```
