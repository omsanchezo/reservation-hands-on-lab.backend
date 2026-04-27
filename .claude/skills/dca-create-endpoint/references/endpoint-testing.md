# Endpoint Testing Reference

## Two Test Types

### 1. Integration Tests (Endpoint Tests)

Test the full HTTP flow: request -> endpoint -> use case -> repository -> database -> response.

### 2. Unit Tests (Mapping Profile Tests)

Test AutoMapper configuration validity and individual mapping correctness.

---

## Integration Tests

### EndpointTestBase

All endpoint tests inherit from `EndpointTestBase`:

```csharp
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using {project}.ndbunit;
using {project}.infrastructure.nhibernate;

namespace {project}.webapi.tests;

public abstract class EndpointTestBase
{
    protected internal INDbUnit nDbUnitTest;
    protected internal IConfiguration configuration;
    protected internal IFixture fixture;
    protected internal HttpClient httpClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        Env.Load();
        string? environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        this.configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", true)
            .Build();

        this.fixture = new Fixture().Customize(new AutoMoqCustomization());
        this.fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => this.fixture.Behaviors.Remove(b));
        this.fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [SetUp]
    public void Setup()
    {
        string connectionStringValue = ConnectionStringBuilder.BuildPostgresConnectionString();
        var schema = new AppSchema();
        this.nDbUnitTest = new PostgreSQLNDbUnit(schema, connectionStringValue);
        this.nDbUnitTest.ClearDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        if (this.httpClient != null) this.httpClient.Dispose();
    }

    protected static internal HttpClient CreateClient(
        string authorizedUserName,
        Action<IServiceCollection>? configureServices = null)
    {
        var factory = new CustomWebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(defaultScheme: "TestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            "TestScheme", options => { options.ClaimsIssuer = authorizedUserName; });
                    configureServices?.Invoke(services);
                });
            });
        return factory.CreateClient();
    }

    protected static internal HttpClient CreateClient(
        Action<IServiceCollection>? configureServices = null)
    {
        if (configureServices == null)
            return new CustomWebApplicationFactory<Program>().CreateClient();

        var factory = new CustomWebApplicationFactory<Program>()
           .WithWebHostBuilder(builder =>
           {
               builder.ConfigureTestServices(services => { configureServices(services); });
           });
        return factory.CreateClient();
    }

    protected internal void LoadScenario(string scenarioName)
    {
        var scenariosFolderPath = Environment.GetEnvironmentVariable("SCENARIOS_FOLDER_PATH");
        var scenarioFilePath = Path.Combine(scenariosFolderPath!, $"{scenarioName}.xml");
        this.nDbUnitTest.ClearDatabase();
        var schema = new AppSchema();
        schema.ReadXml(scenarioFilePath);
        this.nDbUnitTest.SeedDatabase(schema);
    }

    protected internal static HttpContent BuildHttpStringContent(object content)
    {
        var json = JsonConvert.SerializeObject(content);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }
}
```

### CustomWebApplicationFactory

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace {project}.webapi;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override services for testing if needed
        });
        builder.UseEnvironment("Testing");
    }
}
```

Used internally by `EndpointTestBase.CreateClient()`.

### TestAuthHandler

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string EMAILCLAIMTYPE = "email";
    private const string USERNAMECLAIMTYPE = "username";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] {
            new Claim(EMAILCLAIMTYPE, this.ClaimsIssuer),
            new Claim(USERNAMECLAIMTYPE, this.ClaimsIssuer),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}
```

The username passed in `CreateClient(username)` becomes the `ClaimsIssuer` which populates the Claims.

---

### Test Structure Template

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Newtonsoft.Json;
using {project}.webapi.features.{feature}.models;

namespace {project}.webapi.tests.features.{feature};

public class {Action}{Entity}EndpointTests : EndpointTestBase
{
    #region Success Tests

    [Test]
    public async Task {Action}{Entity}_With{Condition}_Returns{Expected}()
    {
        // Arrange
        LoadScenario("ScenarioName");
        httpClient = CreateClient("user@example.com");
        var request = new {Action}{Entity}Model.Request { /* properties */ };

        // Act
        var response = await httpClient.{Method}AsJsonAsync("/{entities}", request);

        // Assert - Status Code
        response.StatusCode.Should().Be(HttpStatusCode.{Expected});

        // Assert - Response Body
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<{Action}{Entity}Model.Response>(content);
        result.Should().NotBeNull();

        // Assert - Database (optional)
        var dataSet = this.nDbUnitTest.GetDataSetFromDb();
    }

    #endregion

    #region Failure Tests

    [Test]
    public async Task {Action}{Entity}_WithInvalidData_ReturnsBadRequest()
    {
        // ...
    }

    #endregion
}
```

### Minimum Tests per Endpoint Type

| Endpoint | Minimum Tests | Status Codes to Cover |
|----------|--------------|----------------------|
| CREATE | 4 | 201 (success), 400 (validation), 409 (duplicate), 401 (no auth) |
| GET | 3 | 200 (found), 404 (not found), 401 (no auth) |
| GET MANY AND COUNT | 2 | 200 (with results), 200 (empty), 401 (no auth) |
| UPDATE | 5 | 200 (success), 400 (validation), 404 (not found), 409 (duplicate), 401 (no auth) |
| DELETE | 3 | 204 (success), 404 (not found), 401 (no auth) |

### Create Endpoint Test Example

```csharp
public class CreatePrototypeEndpointTests : EndpointTestBase
{
    [Test]
    public async Task CreatePrototype_WithValidData_ReturnsCreated()
    {
        // Arrange
        LoadScenario("CreatePrototype");
        httpClient = CreateClient("admin@example.com");
        var request = new CreatePrototypeModel.Request
        {
            Number = "PROTO-001",
            IssueDate = DateTime.Now,
            ExpirationDate = DateTime.Now.AddYears(1),
            Status = "Active"
        };

        // Act
        var response = await httpClient.PostAsJsonAsync("/prototypes", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<CreatePrototypeModel.Response>(content);
        result.Should().NotBeNull();
        result!.Prototype.Number.Should().Be("PROTO-001");
    }

    [Test]
    public async Task CreatePrototype_WithDuplicateNumber_ReturnsConflict()
    {
        // Arrange
        LoadScenario("CreatePrototypeWithExisting");
        httpClient = CreateClient("admin@example.com");
        var request = new CreatePrototypeModel.Request { Number = "EXISTING-001" };

        // Act
        var response = await httpClient.PostAsJsonAsync("/prototypes", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreatePrototype_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        httpClient = CreateClient();

        // Act
        var response = await httpClient.PostAsJsonAsync("/prototypes", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

### Get Endpoint Test Example

```csharp
public class GetPrototypeEndpointTests : EndpointTestBase
{
    [Test]
    public async Task GetPrototype_WithExistingId_ReturnsOk()
    {
        // Arrange
        LoadScenario("CreatePrototype");
        httpClient = CreateClient("admin@example.com");
        var dataSet = nDbUnitTest.GetDataSetFromDb();
        var prototypeId = dataSet.Tables["prototypes"].Rows[0]["id"];

        // Act
        var response = await httpClient.GetAsync($"/prototypes/{prototypeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<GetPrototypeModel.Response>(content);
        result.Should().NotBeNull();
        result!.Prototype.Id.Should().Be((Guid)prototypeId);
    }

    [Test]
    public async Task GetPrototype_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        LoadScenario("CreateSandBox");
        httpClient = CreateClient("admin@example.com");

        // Act
        var response = await httpClient.GetAsync($"/prototypes/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPrototype_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        httpClient = CreateClient();

        // Act
        var response = await httpClient.GetAsync($"/prototypes/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

---

## Anti-Patterns

```csharp
// NEVER: Use endpoint under test in Arrange or Assert
// Use LoadScenario() for Arrange, nDbUnitTest.GetDataSetFromDb() for Assert

❌ WRONG:
[Test]
public async Task UpdateUser_Test()
{
    // Arrange - USES ENDPOINT UNDER TEST (WRONG)
    var createResponse = await httpClient.PostAsJsonAsync("/users", createRequest);
    var createdUser = await createResponse.Content.ReadFromJsonAsync<Response>();

    // Act
    await httpClient.PutAsJsonAsync($"/users/{createdUser.Id}", updateRequest);

    // Assert - USES ENDPOINT UNDER TEST (WRONG)
    var getResponse = await httpClient.GetAsync($"/users/{createdUser.Id}");
}

✅ CORRECT:
[Test]
public async Task UpdateUser_Test()
{
    // Arrange - USE SCENARIO
    LoadScenario("CreateUsers");
    var dataSet = nDbUnitTest.GetDataSetFromDb();
    var userId = (Guid)dataSet.Tables["users"].Rows[0]["id"];
    httpClient = CreateClient("admin@example.com");

    // Act - ONLY HERE USE ENDPOINT
    await httpClient.PutAsJsonAsync($"/users/{userId}", updateRequest);

    // Assert - USE NDBUNIT
    var updatedDataSet = nDbUnitTest.GetDataSetFromDb();
    var row = updatedDataSet.Tables["users"].Rows.Cast<DataRow>()
        .First(r => (Guid)r["id"] == userId);
    row["name"].Should().Be(updateRequest.Name);
}

// NEVER: Let tests execute real external services (email, SMS, APIs)
// Mock external services via CreateClient's configureServices parameter:
httpClient = CreateClient("admin@example.com", services =>
{
    var mockEmailService = new Mock<IEmailService>();
    mockEmailService.Setup(s => s.SendAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
    services.AddSingleton(mockEmailService.Object);
});

// NEVER: Create tests that depend on execution order

// NEVER: Hardcode IDs -- get them from scenario data via GetDataSetFromDb()

// NEVER: Skip database verification after write operations (POST, PUT, DELETE)
```

---

## Mapping Profile Tests

### BaseMappingProfileTests

```csharp
using AutoFixture;
using AutoMapper;

public abstract class BaseMappingProfileTests
{
    protected internal IFixture fixture;
    protected internal MapperConfiguration configuration;
    protected internal IMapper mapper;

    [OneTimeSetUp]
    public virtual void OneTimeSetUp()
    {
        this.fixture = new Fixture()
            .WithAutoMoq()
            .WithoutRecursion();

        this.configuration = new MapperConfiguration(ConfigureProfiles);
        this.mapper = this.configuration.CreateMapper();
    }

    [Test]
    public void MappingConfiguration_ShouldBeValid()
    {
        configuration.AssertConfigurationIsValid();
    }

    protected abstract void ConfigureProfiles(
        IMapperConfigurationExpression configuration);
}
```

### Mapping Profile Test Example

```csharp
public class PrototypeMappingProfileTests : BaseMappingProfileTests
{
    protected override void ConfigureProfiles(IMapperConfigurationExpression configuration)
        => configuration.AddProfile<PrototypeMappingProfile>();

    [Test]
    public void PrototypeToPrototypeDto_ShouldMapCorrectly()
    {
        // Arrange
        var prototype = fixture.Create<Prototype>();

        // Act
        var dto = mapper.Map<PrototypeDto>(prototype);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(prototype.Id);
        dto.Number.Should().Be(prototype.Number);
        dto.Status.Should().Be(prototype.Status);
    }

    [Test]
    public void CreateRequestToCommand_ShouldMapCorrectly()
    {
        // Arrange
        var request = fixture.Create<CreatePrototypeModel.Request>();

        // Act
        var command = mapper.Map<CreatePrototypeUseCase.Command>(request);

        // Assert
        command.Should().NotBeNull();
        command.Number.Should().Be(request.Number);
    }
}
```

---

## File Structure

```
tests/{project}.webapi.tests/
├── features/
│   ├── {entity}/
│   │   ├── Create{Entity}EndpointTests.cs
│   │   ├── Get{Entity}EndpointTests.cs
│   │   ├── GetManyAndCount{Entities}EndpointTests.cs
│   │   ├── Update{Entity}EndpointTests.cs
│   │   └── Delete{Entity}EndpointTests.cs
│   └── ...
├── mappingprofiles/
│   ├── BaseMappingProfileTests.cs
│   ├── {Entity}MappingProfileTests.cs
│   └── ...
├── EndpointTestBase.cs
├── CustomWebApplicationFactory.cs
└── TestAuthHandler.cs
```

## Naming Conventions

| Item | Convention | Example |
|------|-----------|---------|
| Test file | `{Action}{Entity}EndpointTests.cs` | `CreatePrototypeEndpointTests.cs` |
| Test class | `{Action}{Entity}EndpointTests` | `CreatePrototypeEndpointTests` |
| Test method | `{Action}{Entity}_With{Condition}_Returns{Expected}` | `CreatePrototype_WithValidData_ReturnsCreated` |
| Profile test file | `{Entity}MappingProfileTests.cs` | `PrototypeMappingProfileTests.cs` |
| Profile test method | `{Source}To{Dest}_ShouldMapCorrectly` | `PrototypeToPrototypeDto_ShouldMapCorrectly` |
