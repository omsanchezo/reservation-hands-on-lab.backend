# Domain Testing Reference

Testing guide for domain components: entities, validators, value objects.

## DomainTestBase Setup

```csharp
using AutoFixture;
using NUnit.Framework;

[TestFixture]
public class DomainTestBase
{
    protected IFixture fixture;

    [SetUp]
    public void BaseSetUp()
    {
        fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
```

**Required packages:** NUnit, FluentAssertions, AutoFixture

**Test class naming:** `{EntityName}Tests`
**Test method naming:** `{Method}_{Scenario}_{ExpectedResult}`

## Minimum Tests Per Entity

### 1. Constructor - empty (defaults)

```csharp
[Test]
public void Constructor_Default_ShouldSetDefaults()
{
    var entity = new User();
    entity.Id.Should().NotBe(Guid.Empty);
    entity.CreationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    entity.Email.Should().BeEmpty();
}
```

### 2. Constructor - parameterized

```csharp
[Test]
public void Constructor_WithParameters_ShouldSetProperties()
{
    var entity = new User("test@mail.com", "Test");
    entity.Email.Should().Be("test@mail.com");
    entity.Name.Should().Be("Test");
}
```

### 3. IsValid - happy path

```csharp
[Test]
public void IsValid_WithValidData_ShouldReturnTrue()
{
    var entity = fixture.Create<User>();
    entity.IsValid().Should().BeTrue();
}
```

### 4. IsValid - negative (one per validation rule)

```csharp
[Test]
public void IsValid_WithEmptyEmail_ShouldReturnFalse()
{
    var entity = fixture.Build<User>()
        .With(x => x.Email, string.Empty)
        .Create();
    entity.IsValid().Should().BeFalse();
}
```

### 5. GetValidator type

```csharp
[Test]
public void GetValidator_ShouldReturnCorrectType()
{
    var entity = new User();
    entity.GetValidator().Should().BeOfType<UserValidator>();
}
```

### 6. Validate error details

```csharp
[Test]
public void Validate_WithEmptyEmail_ShouldReturnErrorWithCode()
{
    var entity = fixture.Build<User>()
        .With(x => x.Email, string.Empty)
        .Create();
    var errors = entity.Validate();
    errors.Should().Contain(e => e.ErrorCode == "Email");
}
```

## Property Validation Patterns

### String Properties (null, empty, length)

```csharp
[Test]
public void IsValid_WhenNameIsNull_ReturnsFalse()
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Name, (string?)null)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Entity should be invalid when required string is null");
}

[Test]
public void IsValid_WhenNameIsEmpty_ReturnsFalse()
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Name, string.Empty)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Entity should be invalid when required string is empty");
}

[Test]
public void IsValid_WithNameExceeding100Characters_ShouldReturnFalse()
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Name, new string('A', 101))
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Name exceeding 100 characters should be invalid");
}

[Test]
public void IsValid_WithNameAt100Characters_ShouldReturnTrue()
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Name, new string('A', 100))
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeTrue("Name with exactly 100 characters should be valid");
}
```

### DateTime Properties

```csharp
[Test]
public void IsValid_WithDefaultDateTime_ShouldReturnFalse()
{
    // Arrange
    var entity = fixture.Build<Certificate>()
        .With(x => x.IssueDate, default(DateTime))
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("IssueDate should not be default value");
}

[Test]
public void IsValid_WhenIssueDateIsFuture_ReturnsFalse()
{
    // Arrange
    var entity = fixture.Build<Certificate>()
        .With(x => x.IssueDate, DateTime.Today.AddDays(1))
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Entity should be invalid when IssueDate is in the future");
}
```

### Enum / Allowed Values Properties

```csharp
[TestCase("Active")]
[TestCase("Expired")]
[TestCase("Cancelled")]
public void IsValid_WhenStatusIsAllowed_ReturnsTrue(string validStatus)
{
    // Arrange
    var entity = fixture.Build<Prototype>()
        .With(x => x.Status, validStatus)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeTrue($"Entity should be valid when Status is '{validStatus}'");
}

[TestCase("")]
[TestCase("Invalid")]
[TestCase("Pending")]
public void IsValid_WhenStatusIsNotAllowed_ReturnsFalse(string invalidStatus)
{
    // Arrange
    var entity = fixture.Build<Prototype>()
        .With(x => x.Status, invalidStatus)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse($"Entity should be invalid when Status is '{invalidStatus}'");
}
```

### Nullable Properties

```csharp
[Test]
public void IsValid_WithNullOptionalProperty_ShouldReturnTrue()
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.MiddleName, (string?)null)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeTrue("Optional property can be null");
}
```

### Collection Properties

```csharp
[Test]
public void IsValid_WhenCollectionIsEmpty_ReturnsTrue()
{
    // Arrange
    var entity = fixture.Build<User>().Create();
    entity.Roles.Clear();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeTrue("Entity should be valid with empty collection");
}

[Test]
public void IsValid_WhenCollectionIsNull_ReturnsFalse()
{
    // Arrange
    var entity = fixture.Build<User>().Create();
    entity.Roles = null!;

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Entity should be invalid when collection is null");
}
```

## Business Rules Patterns

### Cross-Property Validation

```csharp
[Test]
public void IsValid_WhenExpirationDateIsBeforeIssueDate_ReturnsFalse()
{
    // Arrange
    var entity = fixture.Build<Prototype>()
        .With(x => x.IssueDate, DateTime.Today)
        .With(x => x.ExpirationDate, DateTime.Today.AddDays(-1))
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse("Entity should be invalid when ExpirationDate is before IssueDate");
}
```

### Email Format Validation

```csharp
[TestCase("invalid.email")]
[TestCase("invalid@")]
[TestCase("@invalid.com")]
[TestCase("spaces in@email.com")]
public void IsValid_WhenEmailFormatIsInvalid_ReturnsFalse(string invalidEmail)
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Email, invalidEmail)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeFalse($"Entity should be invalid when Email format is incorrect: {invalidEmail}");
}

[TestCase("valid@example.com")]
[TestCase("user.name@domain.com")]
[TestCase("user+tag@example.co.uk")]
public void IsValid_WhenEmailFormatIsValid_ReturnsTrue(string validEmail)
{
    // Arrange
    var entity = fixture.Build<User>()
        .With(x => x.Email, validEmail)
        .Create();

    // Act
    var result = entity.IsValid();

    // Assert
    result.Should().BeTrue($"Entity should be valid when Email format is correct: {validEmail}");
}
```

## AutoFixture Patterns

- `fixture.Create<T>()` -- random valid instance
- `fixture.Build<T>().With(x => x.Prop, value).Create()` -- customized
- `fixture.Build<T>().Without(x => x.Navigation).Create()` -- skip navigation
- `OmitOnRecursionBehavior` handles circular references

## Edge Case Patterns

- Null string, empty string, whitespace for required strings
- `Guid.Empty` for ID references
- `DateTime.MinValue` / `default(DateTime)` for required dates
- Future dates when past/present is required
- Empty collections vs null collections
- Boundary values: max length exact, max length + 1

## Anti-Patterns

```csharp
// WRONG: Multiple scenarios in one test (no AAA separation)
[Test]
public void TestRoleValidation()
{
    var role = new Role();
    role.Name = null!;
    Assert.IsFalse(role.IsValid());
    role.Name = "Admin";
    Assert.IsTrue(role.IsValid());  // Multiple acts in one test
}

// WRONG: Unrelated assertions mixed together
[Test]
public void TestUserProperties()
{
    var user = new User();
    user.Id.Should().NotBeEmpty();
    user.Email = "invalid";
    user.IsValid().Should().BeFalse();  // Mixes constructor test with validation test
}

// WRONG: No descriptive assertion messages
result.Should().BeFalse();  // When this fails: "Expected result to be false, but found true."

// CORRECT: Descriptive assertion messages
result.Should().BeFalse("Role should be invalid when Name is empty");
// When this fails: "Expected result to be false because Role should be invalid when Name is empty, but found true."
```

**Rules:**
- One logical scenario per test
- Follow AAA pattern with `// Arrange`, `// Act`, `// Assert` comments
- Always include descriptive messages in assertions (the "because" parameter)
- Use FluentAssertions, not `Assert.IsTrue/IsFalse`

## Organization

One test class per entity in `Domain.Tests/Entities/{EntityName}Tests.cs`

## Checklist

- [ ] Test class inherits DomainTestBase
- [ ] Empty constructor test (defaults)
- [ ] Parameterized constructor test
- [ ] IsValid happy path test
- [ ] IsValid negative test for **each** validation rule
- [ ] GetValidator type test
- [ ] Validate error detail tests
- [ ] String property tests (null, empty, max length, boundary)
- [ ] DateTime property tests (default, future/past, ranges)
- [ ] Enum/allowed values tests (valid, invalid, null)
- [ ] Cross-property validations (date ranges, dependent fields)
- [ ] Email format validation (if applicable)
- [ ] Collection property tests (empty, null)
- [ ] AAA pattern in all tests
- [ ] Descriptive assertion messages
- [ ] Descriptive test names: `{Method}_{Scenario}_{ExpectedResult}`
