# Application Testing Reference

Complete reference for unit testing Application Layer handlers: base class setup, Moq patterns for IUnitOfWork and ILogger, minimum tests per handler type, and complete test example.

---

## ApplicationTestBase

```csharp
using AutoFixture;
using AutoFixture.AutoMoq;

namespace {ProjectName}.application.tests;

public class ApplicationTestBase
{
    protected IFixture _fixture = null!;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        // Handle circular references
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
```

**Features:**
- `AutoMoqCustomization`: auto-generates mocks for interfaces
- `OmitOnRecursionBehavior`: handles circular references in domain entities
- All test classes inherit from this base

---

## Moq Patterns for Common Dependencies

### IUnitOfWork Mock

```csharp
var uoWMock = new Mock<IUnitOfWork>();

// Setup repository access
uoWMock.Setup(u => u.Users).Returns(userRepositoryMock.Object);

// Setup transaction methods (void methods -- no Returns needed)
uoWMock.Setup(u => u.BeginTransaction());
uoWMock.Setup(u => u.Commit());
uoWMock.Setup(u => u.Rollback());
```

### ILogger Mock

```csharp
var loggerMock = new Mock<ILogger<CreateUserUseCase.Handler>>();
// No setup needed -- logger calls are void and don't affect behavior
```

### Repository Mock

```csharp
var userRepositoryMock = new Mock<IUserRepository>();

// Setup for Create
userRepositoryMock
    .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
    .ReturnsAsync(expectedUser);

// Setup for Get
userRepositoryMock
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedUser);

// Setup for Get returning null (not found)
userRepositoryMock
    .Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
    .ReturnsAsync((User?)null);

// Setup for throwing exception
userRepositoryMock
    .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
    .ThrowsAsync(new DuplicatedDomainException("User already exists"));
```

---

## Test Naming Convention

```
{Method}_{Scenario}_{Expected}
```

Examples:
- `ExecuteAsync_WithValidCommand_ReturnsOkWithEntity`
- `ExecuteAsync_WithNonExistentUser_ReturnsFailWithNotFound`
- `ExecuteAsync_WhenRepositoryThrowsDuplicated_ReturnsFailWithMessage`
- `ExecuteAsync_WithValidCommand_CallsCommit`
- `ExecuteAsync_WhenExceptionThrown_CallsRollback`

---

## Minimum Tests per Handler Type

### Create Handler Tests

1. Happy path: valid command returns `Result.Ok(entity)`
2. Validation error: `InvalidDomainException` returns `Result.Fail`
3. Duplicate error: `DuplicatedDomainException` returns `Result.Fail`
4. Verify `Commit()` called on success
5. Verify `Rollback()` called on exception

### Get Handler Tests

1. Happy path: entity found returns `Result.Ok(entity)`
2. Not found: returns `Result.Fail` with not-found error
3. Unexpected error: exception returns `Result.Fail`

### GetManyAndCount Handler Tests

1. Happy path: returns `GetManyAndCountResult` with items
2. Empty result: returns result with zero count
3. Verify `Commit()` called on success
4. Verify `Rollback()` called on exception

### Update Handler Tests

1. Happy path: valid command returns `Result.Ok(updated)`
2. Not found: `ResourceNotFoundException` returns `Result.Fail`
3. Validation error: `InvalidDomainException` returns `Result.Fail`
4. Verify `Commit()` called on success
5. Verify `Rollback()` called on exception

### Delete Handler Tests

1. Happy path: valid ID returns `Result.Ok()`
2. Not found: `ResourceNotFoundException` returns `Result.Fail`
3. Verify `Commit()` called on success
4. Verify `Rollback()` called on exception

---

## Complete Test Example: CreateUserHandler

```csharp
using FluentAssertions;
using FluentResults;
using hashira.stone.backend.application.usecases.users;
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.exceptions;
using hashira.stone.backend.domain.interfaces.repositories;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace hashira.stone.backend.application.tests.usecases.users;

[TestFixture]
public class CreateUserHandlerTests : ApplicationTestBase
{
    private Mock<IUnitOfWork> _uoWMock = null!;
    private Mock<IUserRepository> _userRepositoryMock = null!;
    private Mock<ILogger<CreateUserUseCase.Handler>> _loggerMock = null!;
    private CreateUserUseCase.Handler _handler = null!;

    [SetUp]
    public new void Setup()
    {
        base.Setup();

        _uoWMock = new Mock<IUnitOfWork>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<CreateUserUseCase.Handler>>();

        _uoWMock.Setup(u => u.Users).Returns(_userRepositoryMock.Object);

        _handler = new CreateUserUseCase.Handler(
            _uoWMock.Object,
            _loggerMock.Object
        );
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_ReturnsOkWithUser()
    {
        // Arrange
        var expectedUser = _fixture.Create<User>();
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedUser);

        var command = new CreateUserUseCase.Command
        {
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        var result = await _handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedUser);
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_CallsCommit()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<User>());

        var command = new CreateUserUseCase.Command
        {
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        await _handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        _uoWMock.Verify(u => u.BeginTransaction(), Times.Once);
        _uoWMock.Verify(u => u.Commit(), Times.Once);
        _uoWMock.Verify(u => u.Rollback(), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenDuplicateException_ReturnsFailAndRollsBack()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new DuplicatedDomainException("User already exists"));

        var command = new CreateUserUseCase.Command
        {
            Email = "duplicate@example.com",
            Name = "Duplicate User"
        };

        // Act
        var result = await _handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.First().Message.Should().Contain("already exists");
        _uoWMock.Verify(u => u.Rollback(), Times.Once);
        _uoWMock.Verify(u => u.Commit(), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsFailWithValidationError()
    {
        // Arrange
        var validationJson = "[{\"ErrorMessage\":\"Email is invalid\",\"ErrorCode\":\"Email\",\"PropertyName\":\"Email\"}]";
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidDomainException(validationJson));

        var command = new CreateUserUseCase.Command
        {
            Email = "invalid",
            Name = "Test User"
        };

        // Act
        var result = await _handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.First().Message.Should().Be("Email is invalid");
        _uoWMock.Verify(u => u.Rollback(), Times.Once);
    }
}
```

---

## Testing Frameworks

| Library | Version | Purpose |
|---------|---------|---------|
| NUnit | 4.2+ | Test runner and framework |
| Moq | 4.20+ | Mocking framework |
| FluentAssertions | 8.5+ | Expressive assertions |
| AutoFixture | 4.18+ | Test data generation |
| AutoFixture.AutoMoq | 4.18+ | Moq + AutoFixture integration |

---

## Test Structure: AAA Pattern

Every test follows Arrange-Act-Assert with comments:

```csharp
[Test]
public async Task ExecuteAsync_WithValidCommand_ReturnsOkWithEntity()
{
    // Arrange
    var command = new CreateEntityUseCase.Command { ... };
    _repositoryMock.Setup(...).ReturnsAsync(expectedEntity);

    // Act
    var result = await _handler.ExecuteAsync(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be(expectedEntity);
}
```

---

## Common Assertions for Handler Tests

```csharp
// Success assertions
result.IsSuccess.Should().BeTrue();
result.Value.Should().Be(expectedEntity);
result.Value.Should().NotBeNull();

// Failure assertions
result.IsFailed.Should().BeTrue();
result.Errors.Should().NotBeEmpty();
result.Errors.First().Message.Should().Contain("not found");

// Verification assertions
_uoWMock.Verify(u => u.BeginTransaction(), Times.Once);
_uoWMock.Verify(u => u.Commit(), Times.Once);
_uoWMock.Verify(u => u.Rollback(), Times.Never);

// Repository call verification
_repositoryMock.Verify(
    r => r.CreateAsync(It.Is<string>(s => s == "test@example.com"), It.IsAny<string>()),
    Times.Once);
```
