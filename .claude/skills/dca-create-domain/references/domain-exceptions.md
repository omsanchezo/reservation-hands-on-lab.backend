# Domain Exceptions Reference

Four domain exception types with their HTTP mappings and constructor patterns.

## Exception Types

### 1. InvalidDomainException -> HTTP 400

- Constructor 1: `InvalidDomainException(IEnumerable<ValidationFailure> errors)` -- from entity validation
- Constructor 2: `InvalidDomainException(string property, string errorCode, string errorMessage)` -- manual
- Has `Serialize()` method that converts to JSON

### 2. DuplicatedDomainException -> HTTP 409

- Constructor: `DuplicatedDomainException(string message)`
- Simple exception for duplicate entities

### 3. ResourceNotFoundException -> HTTP 404

- Constructor: `ResourceNotFoundException(string message)`
- For missing resources

### 4. InvalidFilterArgumentException -> HTTP 400

- Inherits from `ArgumentException`
- Constructor: `InvalidFilterArgumentException(string message)`
- For invalid query filters

## Usage Patterns

```csharp
// Validation failure
if (!entity.IsValid())
    throw new InvalidDomainException(entity.Validate());

// Duplicate check
var existing = await repository.GetByEmail(email);
if (existing != null)
    throw new DuplicatedDomainException($"User with email {email} already exists");

// Not found
var entity = await repository.GetAsync(id);
if (entity == null)
    throw new ResourceNotFoundException($"User with id {id} not found");

// Invalid filter
if (string.IsNullOrEmpty(filter.Status))
    throw new InvalidFilterArgumentException("Status filter is required");
```

## Endpoint Handling Pattern

```csharp
try { /* use case execution */ }
catch (InvalidDomainException ex)    { return Results.BadRequest(ex.Serialize()); }
catch (DuplicatedDomainException ex) { return Results.Conflict(ex.Message); }
catch (ResourceNotFoundException ex) { return Results.NotFound(ex.Message); }
catch (Exception ex)                 { return Results.InternalServerError(ex.Message); }
```

## Location

`{project}.domain.exceptions`
