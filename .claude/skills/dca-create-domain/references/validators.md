# FluentValidation Validators Reference

> **Scope:** APSYS domain entity validators using FluentValidation.
> **Layer:** Domain (`domain/entities/validators/`)

---

## Pattern

- One validator per entity
- Inherit from `AbstractValidator<T>`
- Namespace: `{project}.domain.entities.validators`
- Location: `domain/entities/validators/`

---

## Base Structure

```csharp
using FluentValidation;

namespace {project}.domain.entities.validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Email)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Email] cannot be null or empty")
            .WithErrorCode("Email")
            .EmailAddress()
            .WithMessage("The [Email] is not a valid email address")
            .WithErrorCode("Email_InvalidDomain");

        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Name] cannot be null or empty")
            .WithErrorCode("Name");
    }
}
```

---

## Common Validations

| Type | Validations |
|------|-------------|
| **String** | `NotEmpty()`, `MaximumLength(n)`, `MinimumLength(n)`, `EmailAddress()`, `Matches(regex)`, `Length(min, max)` |
| **Number** | `GreaterThan(n)`, `LessThan(n)`, `GreaterThanOrEqualTo(n)`, `LessThanOrEqualTo(n)`, `InclusiveBetween(a, b)` |
| **DateTime** | `LessThan(DateTime.Today)`, `GreaterThan(x => x.OtherDate)` |
| **Collections** | `NotEmpty()`, `RuleForEach().SetValidator()` |
| **Enum** | `IsInEnum()` |

---

## Custom Validations

```csharp
// Must() with access to full instance (cross-property validation)
RuleFor(x => x.ExpirationDate)
    .Must((prototype, expirationDate) => expirationDate > prototype.IssueDate)
    .WithMessage("ExpirationDate must be after IssueDate");

// Allowed values
RuleFor(x => x.Status)
    .Must(status => new[] { "Active", "Inactive", "Pending" }.Contains(status))
    .WithMessage("Status must be Active, Inactive, or Pending");

// Conditional validation
RuleFor(x => x.ShippingAddress)
    .NotEmpty()
    .When(x => x.RequiresShipping);
```

---

## WithMessage + WithErrorCode Rules

**ALWAYS** use both `WithMessage()` and `WithErrorCode()` on every validation rule.

**Pattern:** chain per validation group.

```csharp
RuleFor(x => x.Status)
    .NotEmpty()
    .WithMessage("The [Status] cannot be null or empty")   // group 1
    .WithErrorCode("Status")
    .Must(s => AllowedStatuses.Contains(s))
    .WithMessage("The [Status] must be one of: Active, Inactive")  // group 2
    .WithErrorCode("Status_InvalidValue");
```

| Aspect | Format | Example |
|--------|--------|---------|
| **Message** | `"The [{PropertyName}] {description of error}"` | `"The [Email] cannot be null or empty"` |
| **ErrorCode (base)** | `"PropertyName"` | `"Email"` |
| **ErrorCode (specific)** | `"PropertyName_SpecificError"` | `"Email_InvalidDomain"` |

---

## Resources for Constants

Avoid magic strings by defining resource classes for allowed values.

```csharp
public static class TechnicalStandardResource
{
    public const string StatusActive = "Active";
    public const string StatusDeprecated = "Deprecated";
    public static readonly string[] ValidStatuses = { StatusActive, StatusDeprecated };
}

// Usage in validator
RuleFor(x => x.Status)
    .Must(status => TechnicalStandardResource.ValidStatuses.Contains(status))
    .WithMessage("The [Status] must be either 'Active' or 'Deprecated'")
    .WithErrorCode("Status");
```

---

## Complete Examples

### RoleValidator (simple)

```csharp
public class RoleValidator : AbstractValidator<Role>
{
    public RoleValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Name] cannot be null or empty")
            .WithErrorCode("Name");
    }
}
```

### PrototypeValidator (complex -- cross-property, allowed values, dates)

```csharp
public class PrototypeValidator : AbstractValidator<Prototype>
{
    public PrototypeValidator()
    {
        RuleFor(x => x.Number)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Number] cannot be null or empty")
            .WithErrorCode("Number");

        RuleFor(x => x.IssueDate)
            .Must(date => date <= DateTime.Today)
            .WithMessage("The [IssueDate] cannot be in the future")
            .WithErrorCode("IssueDate");

        RuleFor(x => x.ExpirationDate)
            .Must((prototype, expirationDate) => expirationDate > prototype.IssueDate)
            .WithMessage("The [ExpirationDate] must be after the [IssueDate]")
            .WithErrorCode("ExpirationDate");

        RuleFor(x => x.Status)
            .NotNull()
            .NotEmpty()
            .WithMessage("The [Status] cannot be null or empty")
            .WithErrorCode("Status")
            .Must(status => PrototypeResources.ValidStatus.Contains(status))
            .WithMessage("The [Status] must be one of: Active, Expired, Cancelled")
            .WithErrorCode("Status_InvalidValue");
    }
}
```

---

## Integration with Entity

```csharp
// Entity references validator via GetValidator() override
public override IValidator GetValidator() => new UserValidator();

// Usage: validate before persisting
var user = new User("test@example.com", "Test");
if (!user.IsValid())
{
    var errors = user.Validate();
    throw new InvalidDomainException(errors);
}
```

---

## Checklist

- [ ] Inherits `AbstractValidator<Entity>`
- [ ] Namespace: `{project}.domain.entities.validators`
- [ ] `RuleFor()` for each validated property
- [ ] `WithMessage()` + `WithErrorCode()` on every rule
- [ ] Resources for constant values (no magic strings)
- [ ] Integrated with entity via `GetValidator()`
- [ ] No database queries or complex business logic in validator
