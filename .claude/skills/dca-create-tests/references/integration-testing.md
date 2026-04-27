# Integration Testing Reference

Complete reference for NHibernate repository integration tests in APSYS backend projects.

---

## Test Class Hierarchy

```
NHRepositoryTestInfrastructureBase       <- Shared setup: SessionFactory, NDbUnit, AutoFixture, ServiceProvider
        |
NHRepositoryTestBase<TRepo, T, TKey>    <- Per-test setup: RepositoryUnderTest, ClearDatabase
        |
NHUserRepositoryTests                    <- Specific test methods
```

---

## NHRepositoryTestInfrastructureBase (Full Code)

```csharp
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using System.Configuration;
using DotNetEnv;
using {project}.ndbunit;
using {project}.common.tests;
using {project}.domain.entities;
using FluentValidation;
using {project}.domain.entities.validators;
using {project}.infrastructure.nhibernate;

namespace {project}.infrastructure.tests.nhibernate;

public abstract class NHRepositoryTestInfrastructureBase
{
    protected internal ISessionFactory _sessionFactory;
    protected internal INDbUnit nDbUnitTest;
    protected internal IFixture fixture;
    protected internal ServiceProvider _serviceProvider;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        Env.Load();
        string connectionStringValue = ConnectionStringBuilder.BuildPostgresConnectionString();
        var nHSessionFactory = new NHSessionFactory(connectionStringValue);
        this._sessionFactory = nHSessionFactory.BuildNHibernateSessionFactory();
        this.fixture = new Fixture().Customize(new AutoMoqCustomization());
        this.fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => this.fixture.Behaviors.Remove(b));
        this.fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        var schema = new AppSchema();
        this.nDbUnitTest = new PostgreSQLNDbUnit(schema, connectionStringValue);
        var services = new ServiceCollection();
        LoadValidators(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void LoadValidators(ServiceCollection services)
    {
        // Register validators for each domain entity
        // services.AddTransient<AbstractValidator<Role>, RoleValidator>();
        // services.AddTransient<AbstractValidator<User>, UserValidator>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        this._sessionFactory.Dispose();
        this._serviceProvider.Dispose();
    }

    protected internal void LoadScenario(string scenarioName)
    {
        var scenariosFolderPath = Environment.GetEnvironmentVariable("SCENARIOS_FOLDER_PATH");
        if (string.IsNullOrEmpty(scenariosFolderPath))
            throw new ConfigurationErrorsException(
                "No [SCENARIOS_FOLDER_PATH] value found in the .env file");
        var xmlFilePath = Path.Combine(scenariosFolderPath, $"{scenarioName}.xml");
        if (!File.Exists(xmlFilePath))
            throw new FileNotFoundException(
                $"No scenario file build found in [{xmlFilePath}]");
        this.nDbUnitTest.ClearDatabase();
        var schema = new AppSchema();
        schema.ReadXml(xmlFilePath);
        this.nDbUnitTest.SeedDatabase(schema);
    }
}
```

---

## NHRepositoryTestBase (Full Code)

```csharp
using {project}.infrastructure.nhibernate;

namespace {project}.infrastructure.tests.nhibernate;

public abstract class NHRepositoryTestBase<TRepo, T, TKey> : NHRepositoryTestInfrastructureBase
    where T : class, new()
    where TRepo : NHRepository<T, TKey>
{
    public TRepo RepositoryUnderTest { get; protected set; }

    [SetUp]
    public void Setup()
    {
        this.RepositoryUnderTest = this.BuildRepository();
        this.nDbUnitTest.ClearDatabase();
    }

    abstract protected internal TRepo BuildRepository();
}
```

---

## Cardinal Rule

**NEVER use the repository under test in Arrange or Assert phases.** Only use it in the Act phase.

| Phase | What to use | Why |
|-------|-------------|-----|
| **Arrange** | `LoadScenario("scenario_name")` | Seed data via NDbUnit XML, not the repo |
| **Act** | `RepositoryUnderTest.Method()` | This is what we're testing |
| **Assert** | `nDbUnitTest.GetDataSetFromDb()` | Verify DB state independently of repo |

```csharp
// CORRECT
[Test]
public async Task CreateAsync_ValidData_CreatesEntity()
{
    // Arrange: Use LoadScenario, NOT the repository
    LoadScenario("Sc010CreateSandBox");

    // Act: Use the repository under test
    var result = await RepositoryUnderTest.CreateAsync("test@email.com", "Test User");

    // Assert: Verify in DB via NDbUnit, NOT the repository
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var usersTable = dataSet.Tables["users"];
    usersTable.Rows.Count.Should().Be(1, "one user should exist in the database");
}

// WRONG: Using repository in Arrange
[Test]
public async Task UpdateAsync_ValidData_UpdatesEntity()
{
    // WRONG: Arranging with the repo under test
    await RepositoryUnderTest.CreateAsync("test@email.com", "Old Name");

    // Act
    await RepositoryUnderTest.UpdateNameAsync(id, "New Name");

    // Assert...
}
```

---

## AAA Pattern for Repository Tests

```
Arrange:  LoadScenario("scenario_name")   <- Seeds DB with known state
Act:      RepositoryUnderTest.Method()     <- Execute the method being tested
Assert:   nDbUnitTest.GetDataSetFromDb()   <- Verify DB state directly
```

---

## Test Naming Convention

```
[MethodName]_[Scenario]_[ExpectedBehavior]
```

Examples:
- `CreateAsync_ValidData_CreatesEntityInDatabase`
- `CreateAsync_DuplicateEmail_ThrowsDuplicatedDomainException`
- `CreateAsync_InvalidName_ThrowsInvalidDomainException`
- `GetByEmailAsync_ExistingEmail_ReturnsUser`
- `GetByEmailAsync_NonExistentEmail_ReturnsNull`
- `UpdateAsync_EntityNotFound_ThrowsResourceNotFoundException`
- `DeleteAsync_ExistingEntity_RemovesFromDatabase`

---

## Minimum Test Coverage Per Method

| Method | Minimum Tests |
|--------|---------------|
| `CreateAsync` | Happy path + required field validation + duplicate + format validation |
| `GetByXXXAsync` | Existing entity + non-existent + null/empty input |
| `UpdateAsync` | Happy path + not found + duplicate (exclude self) |
| `DeleteAsync` | Happy path + verify DB |

---

## Building the Repository in Tests

```csharp
public class NHUserRepositoryTests
    : NHRepositoryTestBase<NHUserRepository, User, Guid>
{
    protected internal override NHUserRepository BuildRepository()
    {
        // Open a new session from the shared SessionFactory
        var session = _sessionFactory.OpenSession();
        return new NHUserRepository(session, _serviceProvider);
    }
}
```

For read-only repository tests, use a different base or adapt:

```csharp
public class NHOrderDaoRepositoryTests : NHRepositoryTestInfrastructureBase
{
    private NHReadOnlyRepository<OrderDao, Guid> _repository;

    [SetUp]
    public void Setup()
    {
        var session = _sessionFactory.OpenSession();
        _repository = new NHReadOnlyRepository<OrderDao, Guid>(session);
        nDbUnitTest.ClearDatabase();
    }
}
```

---

## Complete Examples

### Example 1: CRUD Repository Tests

```csharp
[TestFixture]
public class NHUserRepositoryTests
    : NHRepositoryTestBase<NHUserRepository, User, Guid>
{
    protected internal override NHUserRepository BuildRepository()
    {
        var session = _sessionFactory.OpenSession();
        return new NHUserRepository(session, _serviceProvider);
    }

    #region CreateAsync

    [Test]
    public async Task CreateAsync_ValidData_CreatesUserInDatabase()
    {
        // Arrange
        LoadScenario("Sc010CreateSandBox");
        var email = "john@example.com";
        var name = "John Doe";

        // Act
        var result = await RepositoryUnderTest.CreateAsync(email, name);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.Name.Should().Be(name);
        result.Id.Should().NotBeEmpty();

        var dataSet = nDbUnitTest.GetDataSetFromDb();
        var usersTable = dataSet.Tables["users"];
        usersTable.Rows.Count.Should().Be(1, "one user should exist in the database");
    }

    [Test]
    public async Task CreateAsync_DuplicateEmail_ThrowsDuplicatedDomainException()
    {
        // Arrange
        LoadScenario("Sc020CreateUsers");  // Has existing user with email

        // Act
        var act = async () => await RepositoryUnderTest.CreateAsync(
            "existing@example.com", "New User");

        // Assert
        await act.Should().ThrowAsync<DuplicatedDomainException>()
            .WithMessage("*already exists*");
    }

    [Test]
    public async Task CreateAsync_EmptyEmail_ThrowsInvalidDomainException()
    {
        // Arrange
        LoadScenario("Sc010CreateSandBox");

        // Act
        var act = async () => await RepositoryUnderTest.CreateAsync("", "John");

        // Assert
        await act.Should().ThrowAsync<InvalidDomainException>();
    }

    #endregion

    #region GetByEmailAsync

    [Test]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        // Arrange
        LoadScenario("Sc020CreateUsers");

        // Act
        var result = await RepositoryUnderTest.GetByEmailAsync("existing@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("existing@example.com");
    }

    [Test]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        // Arrange
        LoadScenario("Sc010CreateSandBox");

        // Act
        var result = await RepositoryUnderTest.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByEmailAsync_NullEmail_ReturnsNull()
    {
        // Arrange
        LoadScenario("Sc010CreateSandBox");

        // Act
        var result = await RepositoryUnderTest.GetByEmailAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
```

### Example 2: Read-Only Repository Tests

```csharp
[TestFixture]
public class NHOrderDaoRepositoryTests : NHRepositoryTestInfrastructureBase
{
    private NHReadOnlyRepository<OrderDao, Guid> _repository;

    [SetUp]
    public void Setup()
    {
        var session = _sessionFactory.OpenSession();
        _repository = new NHReadOnlyRepository<OrderDao, Guid>(session);
        nDbUnitTest.ClearDatabase();
    }

    [Test]
    public async Task GetManyAndCountAsync_WithData_ReturnsCorrectCount()
    {
        // Arrange
        LoadScenario("Sc030CreateOrders");

        // Act
        var result = await _repository.GetManyAndCountAsync(
            null,
            nameof(OrderDao.OrderNumber));

        // Assert
        result.Total.Should().BeGreaterThan(0);
        result.Items.Should().NotBeEmpty();
    }

    [Test]
    public async Task CountAsync_EmptyDatabase_ReturnsZero()
    {
        // Arrange
        LoadScenario("Sc010CreateSandBox");

        // Act
        var count = await _repository.CountAsync();

        // Assert
        count.Should().Be(0);
    }
}
```

---

## Update and Delete Test Patterns

### UpdateAsync Tests

```csharp
#region UpdateAsync

[Test]
public async Task UpdateAsync_WithValidParameters_ShouldUpdateInDatabase()
{
    // Arrange
    LoadScenario("Sc020CreateUsers");
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var userId = (Guid)dataSet.Tables["users"].Rows[0]["id"];

    // Act
    await RepositoryUnderTest.UpdateAsync(userId, "updated@example.com", "Updated Name");

    // Assert - Verify in database using NDbUnit
    var updatedDataSet = nDbUnitTest.GetDataSetFromDb();
    var updatedRows = updatedDataSet.Tables["users"].Select($"id = '{userId}'");
    updatedRows.Length.Should().Be(1);
    updatedRows[0]["email"].Should().Be("updated@example.com");
    updatedRows[0]["name"].Should().Be("Updated Name");
}

[Test]
public async Task UpdateAsync_WithNonExistingId_ShouldThrowResourceNotFoundException()
{
    // Arrange
    LoadScenario("Sc010CreateSandBox");
    var nonExistingId = Guid.NewGuid();

    // Act
    Func<Task> act = async () => await RepositoryUnderTest.UpdateAsync(
        nonExistingId, "email@example.com", "Name");

    // Assert
    await act.Should().ThrowAsync<ResourceNotFoundException>()
        .WithMessage($"*'{nonExistingId}'*");
}

[Test]
public async Task UpdateAsync_WithDuplicateEmail_ShouldThrowDuplicatedDomainException()
{
    // Arrange - Load scenario with multiple users
    LoadScenario("Sc020CreateUsers");
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var users = dataSet.Tables["users"];
    users.Rows.Count.Should().BeGreaterThan(1, "Precondition: Need at least 2 users");

    var firstUserId = (Guid)users.Rows[0]["id"];
    var secondUserEmail = (string)users.Rows[1]["email"];

    // Act - Try to update first user with second user's email
    Func<Task> act = async () => await RepositoryUnderTest.UpdateAsync(
        firstUserId, secondUserEmail, "Name");

    // Assert
    await act.Should().ThrowAsync<DuplicatedDomainException>();
}

[Test]
public async Task UpdateAsync_WithSameEmail_ShouldSucceed()
{
    // Arrange
    LoadScenario("Sc020CreateUsers");
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var userRow = dataSet.Tables["users"].Rows[0];
    var userId = (Guid)userRow["id"];
    var currentEmail = (string)userRow["email"];

    // Act - Update with same email (no-op for unique constraint)
    await RepositoryUnderTest.UpdateAsync(userId, currentEmail, "New Name");

    // Assert
    var updatedDataSet = nDbUnitTest.GetDataSetFromDb();
    var updatedRow = updatedDataSet.Tables["users"].Select($"id = '{userId}'").First();
    updatedRow["email"].Should().Be(currentEmail);
    updatedRow["name"].Should().Be("New Name");
}

#endregion

#region DeleteAsync

[Test]
public async Task DeleteAsync_ExistingEntity_ShouldRemoveFromDatabase()
{
    // Arrange
    LoadScenario("Sc020CreateUsers");
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var userRow = dataSet.Tables["users"].Rows[0];
    userRow.Should().NotBeNull("Precondition: There should be at least one user");
    var userId = (Guid)userRow["id"];

    // Act
    await RepositoryUnderTest.DeleteAsync(userId);

    // Assert - Verify deletion in database using NDbUnit
    var updatedDataSet = nDbUnitTest.GetDataSetFromDb();
    var deletedRows = updatedDataSet.Tables["users"].Select($"id = '{userId}'");
    deletedRows.Should().BeEmpty();
}

#endregion
```

**What to verify in UpdateAsync:**
- Updated fields have new values
- Non-updated fields retain original values
- Exactly 1 row affected
- Duplicate check excludes the entity being updated (self-update is valid)

**Note on DeleteAsync:** If the repository only exposes `DeleteAsync(TEntity entity)` instead of `DeleteAsync(Guid id)`, it's acceptable to use `GetAsync` in Arrange to obtain the entity. Document this exception clearly in the test.

---

## Test Scenarios (Arrange Tool)

Scenarios are the mechanism to seed the database with known data for the Arrange phase. They are **C# classes that generate XML files** -- the XMLs are never created manually.

### How It Works

```
IScenario class (.cs)          ScenarioBuilder (Program.cs)         XML file
  SeedData() uses repos   -->   ClearDatabase + LoadPreload    -->   Generated artifact
  to insert data                + SeedData + GetDataSetFromDb        loaded by LoadScenario()
```

1. A C# class implements `IScenario` and inserts data using repositories (`_uoW.Users.CreateAsync(...)`)
2. `ScenarioBuilder` executes each class: clears DB, loads prerequisites, runs `SeedData()`, dumps DB to XML
3. Tests call `LoadScenario("ScenarioName")` which loads the generated XML into the test database

### IScenario Interface

```csharp
public interface IScenario
{
    Task SeedData();                // Inserts data using repositories
    string ScenarioFileName { get; } // XML filename (without .xml extension)
    Type? PreloadScenario { get; }   // Scenario to load before this one (dependency)
}
```

### Naming Convention

```
Sc###Create{Entity}.cs

Sc  = Scenario prefix
### = Dependency order number
Create{Entity} = Descriptive name
```

| Range | Use |
|-------|-----|
| `010` | SandBox/Base (empty DB) |
| `020-029` | Root entities (no FK dependencies) |
| `030-039` | Entities with 1 level of dependency |
| `040-049` | Entities with 2 levels of dependency |
| `0X1, 0X2...` | Variants of scenario `0X0` |

### Dependency Chain

Scenarios declare their prerequisites via `PreloadScenario`. The builder resolves the chain automatically:

```
Sc010CreateSandBox (base, empty)
    |
Sc020CreateRoles (no dependencies)
    |
Sc030CreateUsers (depends on Roles)
    |
Sc031CreateAdminUser (depends on Users, which includes Roles)
```

When generating `CreateAdminUser.xml`:
1. Clear DB
2. Load `CreateUsers.xml` (which already includes roles data)
3. Execute `Sc031CreateAdminUser.SeedData()`
4. Dump entire DB state to `CreateAdminUser.xml`

### Example: Scenario Without Dependencies

```csharp
public class Sc020CreateRoles(IUnitOfWork uoW) : IScenario
{
    private readonly IUnitOfWork _uoW = uoW;

    public string ScenarioFileName => "CreateRoles";
    public Type? PreloadScenario => null;

    public async Task SeedData()
    {
        try
        {
            this._uoW.BeginTransaction();
            await this._uoW.Roles.CreateAsync("PlatformAdministrator");
            await this._uoW.Roles.CreateAsync("OrganizationAdmin");
            await this._uoW.Roles.CreateAsync("User");
            this._uoW.Commit();
        }
        catch
        {
            this._uoW.Rollback();
            throw;
        }
    }
}
```

### Example: Scenario With Dependencies

```csharp
public class Sc030CreateUsers(IUnitOfWork uoW) : IScenario
{
    private readonly IUnitOfWork _uoW = uoW;

    public string ScenarioFileName => "CreateUsers";
    public Type? PreloadScenario => typeof(Sc020CreateRoles);  // Requires roles

    public async Task SeedData()
    {
        try
        {
            this._uoW.BeginTransaction();
            await this._uoW.Users.CreateAsync("user1@example.com", "Carlos Rodriguez");
            await this._uoW.Users.CreateAsync("user2@example.com", "Ana Gonzalez");
            this._uoW.Commit();
        }
        catch
        {
            this._uoW.Rollback();
            throw;
        }
    }
}
```

### Example: SandBox (Empty Scenario)

```csharp
public class Sc010CreateSandBox(INDbUnit nDbUnit) : IScenario
{
    private readonly INDbUnit _nDbUnit = nDbUnit;

    public string ScenarioFileName => "CreateSandBox";
    public Type? PreloadScenario => null;

    public async Task SeedData()
    {
        await using var connection = new NpgsqlConnection(_nDbUnit.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var tablesToClean = new[]
            {
                "public.user_in_roles",
                "public.users",
                "public.roles"
            };

            foreach (var table in tablesToClean)
            {
                await using var cmd = new NpgsqlCommand(
                    $"TRUNCATE TABLE {table} CASCADE", connection, transaction);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Generating XMLs

```bash
# Build and run the scenarios project
cd tests/{project}.scenarios
dotnet build
cd bin/Debug/net9.0
{project}.scenarios.exe /cnn:"Host=localhost;Port=5432;Database=mydb_test;Username=postgres;Password=root;" /output:"D:\path\to\scenarios"
```

The `SCENARIOS_FOLDER_PATH` environment variable in the `.env` file must point to the output folder so `LoadScenario()` can find the XMLs.

### Adding a New Scenario

1. Create `Sc###Create{Entity}.cs` implementing `IScenario`
2. Set `PreloadScenario` to the scenario that creates required dependencies
3. Use repositories in `SeedData()` (never raw SQL for data insertion)
4. Register the entity's validator in `ScenarioBuilder` if not already registered
5. Build and run the scenarios project to generate the XML
6. Use `LoadScenario("ScenarioFileName")` in tests

### Scenario Rules

- **Use repositories, not raw SQL** for data insertion in `SeedData()` -- ensures domain validation
- **Always wrap in BeginTransaction/Commit/Rollback** -- same pattern as use cases
- **XMLs are generated artifacts** -- never create or edit them manually
- **XMLs are local, not versioned** -- each developer generates their own
- **Keep scenarios focused** -- one scenario per entity or variant, not monolithic

---

## Anti-Patterns

```csharp
// NEVER: Use repository under test in Arrange
await RepositoryUnderTest.CreateAsync("test@email.com", "Test");  // In Arrange phase

// NEVER: Use repository under test to verify in Assert
var user = await RepositoryUnderTest.GetByEmailAsync("test@email.com");  // In Assert phase
// Use nDbUnitTest.GetDataSetFromDb() instead

// NEVER: Create order-dependent tests (Test A must run before Test B)

// NEVER: Hardcode GUIDs without using ScenarioIds constants

// NEVER: Skip database verification (only checking return value)
```
