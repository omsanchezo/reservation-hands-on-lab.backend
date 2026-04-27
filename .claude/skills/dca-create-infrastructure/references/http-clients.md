# HTTP Client Patterns

## The Problem with `new HttpClient()`

```csharp
// ANTI-PATTERN: Creating new HttpClient per call
private static HttpResponseMessage PostAsync(string requestUri, object? body, string authToken)
{
    using (var httpClient = new HttpClient())  // Socket exhaustion
    {
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        return httpClient.PostAsync(requestUri, content).Result;  // Blocking call
    }
}
```

Issues: socket exhaustion, indefinite DNS caching, thread starvation from `.Result`.

## IHttpClientFactory

Solves connection pooling, DNS refresh, lifecycle management, and Polly integration.

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="9.0+" />
```

## Named HttpClients

```csharp
services.AddHttpClient("Auth0", client =>
{
    client.BaseAddress = new Uri(configuration["Auth0ManagementSettings:Domain"]);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

```csharp
public class Auth0Service : IIdentityService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public Auth0Service(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<User> CreateAsync(string username, string name, string password)
    {
        var client = _httpClientFactory.CreateClient("Auth0");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var body = new { email = username, name, password, connection = GetConnection() };
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v2/users", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<User>(result);
    }
}
```

## Typed HttpClients (Recommended)

```csharp
public class Auth0HttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public Auth0HttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration["Auth0ManagementSettings:Domain"]);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _configuration["Auth0ManagementSettings:ClientId"],
            client_secret = _configuration["Auth0ManagementSettings:ClientSecret"],
            audience = _configuration["Auth0ManagementSettings:Audience"],
            grant_type = _configuration["Auth0ManagementSettings:GrantType"]
        };

        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/oauth/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonConvert.DeserializeAnonymousType(result, new { access_token = "" });
        return tokenResponse?.access_token ?? throw new InvalidOperationException("Failed to get access token");
    }

    public async Task<Auth0UserResponse> CreateUserAsync(
        string email, string name, string password, string accessToken,
        CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new { email, name, password, connection = _configuration["Auth0ManagementSettings:Connection"] };
        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/v2/users", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (errorContent.Contains("user already exists"))
                throw new DuplicatedDomainException($"User with email '{email}' already exists");
            throw new HttpRequestException($"Auth0 API error: {errorContent}");
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<Auth0UserResponse>(result);
    }
}
```

DI registration:

```csharp
services.AddHttpClient<Auth0HttpClient>();
```

## Resilience with Polly

```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0+" />
```

```csharp
services.AddHttpClient<Auth0HttpClient>()
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds}s due to {outcome.Exception?.Message}");
            }
        )
    );
```

Retry + Circuit Breaker:

```csharp
services.AddHttpClient<Auth0HttpClient>()
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
    )
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30)
        )
    );
```

## Advanced Configuration

```csharp
// Custom headers
services.AddHttpClient<Auth0HttpClient>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// Per-request timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await _httpClient.GetAsync("/api/endpoint", cts.Token);

// Compression
services.AddHttpClient<Auth0HttpClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
```

## Error Handling

```csharp
var response = await _httpClient.GetAsync("/api/users");

if (response.StatusCode == HttpStatusCode.NotFound)
    return null;

if (response.StatusCode == HttpStatusCode.Unauthorized)
    throw new UnauthorizedException("Access token expired or invalid");

if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync();
    throw new ExternalServiceException($"API error: {errorContent}");
}

var content = await response.Content.ReadAsStringAsync();
return JsonConvert.DeserializeObject<User>(content);
```

Map API errors to domain exceptions:

```csharp
if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync();
    if (errorContent.Contains("user already exists"))
        throw new DuplicatedDomainException($"User with email '{email}' already exists");
    if (errorContent.Contains("invalid email"))
        throw new InvalidDomainException("Invalid email format");
    throw new ExternalServiceException($"Auth0 API error: {errorContent}");
}
```

## Logging Handler

```csharp
services.AddHttpClient<Auth0HttpClient>()
    .AddHttpMessageHandler<LoggingDelegatingHandler>();

public class LoggingDelegatingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingDelegatingHandler> _logger;

    public LoggingDelegatingHandler(ILogger<LoggingDelegatingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP {Method} {Uri}", request.Method, request.RequestUri);
        var response = await base.SendAsync(request, cancellationToken);
        _logger.LogInformation("HTTP {StatusCode} from {Uri}", (int)response.StatusCode, request.RequestUri);
        return response;
    }
}
```

## Best Practices

- Always use `IHttpClientFactory` -- never `new HttpClient()`
- Always use `async/await` -- never `.Result` or `.Wait()`
- Always pass `CancellationToken`
- Prefer typed clients over named clients for type safety
- Configure `BaseAddress` in DI, use relative URLs in calls
- Do NOT `Dispose` clients from `IHttpClientFactory` -- the factory manages lifecycle
- Add Polly retry policies for transient errors
