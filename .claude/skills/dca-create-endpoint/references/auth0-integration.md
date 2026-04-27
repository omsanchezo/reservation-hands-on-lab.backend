# Auth0 Integration

## Architecture

Auth0 acts as the Identity Provider. The domain layer defines `IIdentityService`; the infrastructure layer implements it with the Auth0.NET SDK.

```
domain/interfaces/services/IIdentityService.cs      <- Interface
infrastructure/services/Auth0Service.cs              <- Real implementation
infrastructure/services/Auth0ServiceMock.cs           <- Mock for dev/testing
webapi/infrastructure/ServiceCollectionExtender.cs    <- DI registration
```

## Configuration

### appsettings.json

```json
{
  "Auth0ManagementSettings": {
    "Domain": "https://tu-tenant.us.auth0.com",
    "ClientId": "abc123...",
    "ClientSecret": "xyz789...",
    "Audience": "https://tu-tenant.us.auth0.com/api/v2/",
    "GrantType": "client_credentials",
    "Connection": "Username-Password-Authentication"
  }
}
```

### Options Pattern with Startup Validation

```csharp
public class Auth0ManagementOptions
{
    public const string SectionName = "Auth0ManagementSettings";

    [Required, Url]
    public string Domain { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required, Url]
    public string Audience { get; set; } = string.Empty;

    [Required]
    public string GrantType { get; set; } = "client_credentials";

    [Required]
    public string Connection { get; set; } = string.Empty;

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
```

## JWT Validation Setup

```csharp
services.AddAuthentication("Bearer")
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = identityServerUrl;  // Auth0 domain
        options.RequireHttpsMetadata = false;   // true in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });
```

## Auth0 Custom Claims (Auth0 Actions)

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://your-api.com/';

  if (event.user.email) {
    api.idToken.setCustomClaim(`${namespace}email`, event.user.email);
    api.idToken.setCustomClaim(`${namespace}username`, event.user.email);
  }

  if (event.user.name) {
    api.idToken.setCustomClaim(`${namespace}name`, event.user.name);
  }

  if (event.authorization && event.authorization.roles) {
    api.idToken.setCustomClaim(`${namespace}roles`, event.authorization.roles);
  }
};
```

## Async Interface

```csharp
public interface IIdentityService
{
    Task<User> CreateAsync(string username, string name, string password,
        CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<User?> ChangePasswordAsync(string userName, string newPassword,
        CancellationToken ct = default);
}
```

## Auth0Service with SDK

Install packages:

```bash
dotnet add package Auth0.ManagementApi
dotnet add package Auth0.AuthenticationApi
```

Implementation:

```csharp
public class Auth0Service : IIdentityService
{
    private readonly Auth0ManagementOptions _options;
    private readonly ILogger<Auth0Service> _logger;
    private readonly IAuthenticationApiClient _authClient;

    public Auth0Service(IOptions<Auth0ManagementOptions> options, ILogger<Auth0Service> logger)
    {
        _options = options.Value;
        _logger = logger;
        _authClient = new AuthenticationApiClient(new Uri(_options.Domain));
    }

    private async Task<IManagementApiClient> GetManagementClientAsync(CancellationToken ct = default)
    {
        var tokenResponse = await _authClient.GetTokenAsync(
            new ClientCredentialsTokenRequest
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
                Audience = _options.Audience
            }, ct);

        return new ManagementApiClient(tokenResponse.AccessToken, new Uri(_options.Domain));
    }

    public async Task<User> CreateAsync(string username, string name, string password,
        CancellationToken ct = default)
    {
        try
        {
            var client = await GetManagementClientAsync(ct);
            var auth0User = await client.Users.CreateAsync(new UserCreateRequest
            {
                Email = username,
                Name = name,
                Password = password,
                Connection = _options.Connection,
                EmailVerified = false
            }, ct);

            _logger.LogInformation("Usuario creado: {UserId} - {Email}",
                auth0User.UserId, auth0User.Email);
            return MapToDomainUser(auth0User);
        }
        catch (Auth0.Core.Exceptions.ApiException ex)
            when (ex.Message.Contains("user already exists"))
        {
            throw new ArgumentException("errors.userEmail.alreadyExist");
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var client = await GetManagementClientAsync(ct);
        var users = await client.Users.GetAllAsync(
            new GetUsersRequest { Query = $"email:\"{email}\"" },
            new PaginationInfo(0, 1, false), ct);

        return users?.Any() == true ? MapToDomainUser(users.First()) : null;
    }

    private static User MapToDomainUser(Auth0.ManagementApi.Models.User auth0User) =>
        new User
        {
            UserId = auth0User.UserId,
            Email = auth0User.Email,
            Name = auth0User.FullName ?? auth0User.NickName ?? auth0User.Email,
            CreationDate = auth0User.CreatedAt ?? DateTime.UtcNow
        };
}
```

## Mock for Development/Testing

```csharp
public class Auth0ServiceMock : IIdentityService
{
    private readonly ILogger<Auth0ServiceMock> _logger;
    private readonly Dictionary<string, User> _users = new();
    private int _userCounter = 1;

    public Auth0ServiceMock(ILogger<Auth0ServiceMock> logger)
    {
        _logger = logger;
        _users["test@example.com"] = new User
        {
            UserId = "auth0|mock-001",
            Email = "test@example.com",
            Name = "Test User",
            CreationDate = DateTime.UtcNow.AddDays(-30)
        };
    }

    public Task<User> CreateAsync(string username, string name, string password,
        CancellationToken ct = default)
    {
        if (_users.ContainsKey(username))
            throw new ArgumentException("errors.userEmail.alreadyExist");

        var user = new User
        {
            UserId = $"auth0|mock-{_userCounter++:D3}",
            Email = username, Name = name,
            CreationDate = DateTime.UtcNow
        };
        _users[username] = user;
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        _users.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }
}
```

## Dependency Injection

```csharp
public static IServiceCollection AddAuth0IdentityService(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    services.AddOptions<Auth0ManagementOptions>()
        .Bind(configuration.GetSection(Auth0ManagementOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();  // Fails at startup if invalid

    if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        services.AddScoped<IIdentityService, Auth0ServiceMock>();
    else
        services.AddScoped<IIdentityService, Auth0Service>();

    return services;
}
```

## Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| Socket exhaustion | `new HttpClient()` per request | Use Auth0.NET SDK or `IHttpClientFactory` |
| Deadlock | `.Result` on async calls | Use `async`/`await` everywhere |
| Rate limit (429) | Too many Management API calls | Add retry with exponential backoff via Polly |
| Token expired | Cached management token | Obtain fresh token per operation via `GetManagementClientAsync` |
| Late validation | Config checked per request | Use `ValidateOnStart()` on options |

## Best Practices

- Use Auth0.NET SDK instead of raw `HttpClient`
- Always `async`/`await` — never `.Result` or `.GetAwaiter().GetResult()`
- Validate configuration at startup with `ValidateDataAnnotations()` + `ValidateOnStart()`
- Use structured logging: `_logger.LogInformation("Created: {UserId}", userId)`
- Convert Auth0 exceptions to domain exceptions at the infrastructure boundary
- Register `Auth0ServiceMock` for Development/Testing environments
