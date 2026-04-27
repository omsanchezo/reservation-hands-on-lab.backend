# Redis Distributed Cache

## When to Use Redis

- Web farm / multiple application instances
- Cache persistence between deployments
- Sharing data between services/applications
- Distributed session state, rate limiting, pub/sub

Do NOT use when: single server (use IMemoryCache), no need to share data between instances.

## Installation

```bash
# IDistributedCache (recommended)
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis

# Direct StackExchange.Redis usage
dotnet add package StackExchange.Redis
```

## Configuration

### Basic with IDistributedCache

```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MyApp_"; // Prefix for all keys
});
```

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=yourpassword,ssl=false,abortConnect=false"
  }
}
```

### Advanced Configuration

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { "localhost:6379" },
        Password = "yourpassword",
        Ssl = false,
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000,
        ConnectRetry = 3,
        KeepAlive = 60,
        DefaultDatabase = 0,
        ClientName = "MyApp"
    };
});
```

### ConnectionMultiplexer (Direct Usage)

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = new ConfigurationOptions
    {
        EndPoints = { "localhost:6379" },
        Password = "yourpassword",
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000,
        AsyncTimeout = 5000,
        ConnectRetry = 3
    };

    var multiplexer = ConnectionMultiplexer.Connect(config);

    multiplexer.ConnectionFailed += (sender, args) =>
        sp.GetRequiredService<ILogger<Program>>()
            .LogError("Redis connection failed: {Exception}", args.Exception);

    multiplexer.ConnectionRestored += (sender, args) =>
        sp.GetRequiredService<ILogger<Program>>()
            .LogInformation("Redis connection restored");

    return multiplexer;
});
```

## IDistributedCache Usage

### Basic Cache-Aside Pattern

```csharp
public class OrderService
{
    private readonly IDistributedCache _cache;

    public async Task<Order> GetOrderByIdAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"order:{id}";

        var cachedOrder = await _cache.GetStringAsync(cacheKey, ct);
        if (cachedOrder != null)
            return JsonSerializer.Deserialize<Order>(cachedOrder);

        var order = await LoadOrderFromDatabaseAsync(id, ct);

        if (order != null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(order), options, ct);
        }

        return order;
    }

    public async Task InvalidateOrderCacheAsync(int orderId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"order:{orderId}", ct);
    }
}
```

### Wrapper Service

```csharp
public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null,
        CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null,
        CancellationToken ct = default)
    {
        var cachedValue = await _cache.GetStringAsync(key, ct);
        if (cachedValue != null)
            return JsonSerializer.Deserialize<T>(cachedValue);

        var value = await factory();

        var options = new DistributedCacheEntryOptions();
        if (absoluteExpiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
        if (slidingExpiration.HasValue)
            options.SlidingExpiration = slidingExpiration.Value;

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, ct);
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(key, ct);
    }
}
```

## StackExchange.Redis Direct Operations

### String Operations

```csharp
public class RedisStringService
{
    private readonly IDatabase _db;

    public RedisStringService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var serialized = JsonSerializer.Serialize(value);
        return await _db.StringSetAsync(key, serialized, expiration);
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value);
    }

    public async Task<long> IncrementAsync(string key, long value = 1)
        => await _db.StringIncrementAsync(key, value);

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiration)
        => await _db.StringSetAsync(key, value, expiration, When.NotExists);

    public async Task<bool> DeleteAsync(string key)
        => await _db.KeyDeleteAsync(key);
}
```

### Hash Operations

```csharp
public async Task SetUserAsync(int userId, User user)
{
    var key = $"user:{userId}";
    var hashEntries = new HashEntry[]
    {
        new HashEntry("id", user.Id),
        new HashEntry("name", user.Name),
        new HashEntry("email", user.Email),
        new HashEntry("createdAt", user.CreatedAt.ToString("O"))
    };

    await _db.HashSetAsync(key, hashEntries);
    await _db.KeyExpireAsync(key, TimeSpan.FromHours(1));
}

public async Task<string> GetUserEmailAsync(int userId)
    => await _db.HashGetAsync($"user:{userId}", "email");

public async Task<long> IncrementLoginCountAsync(int userId)
    => await _db.HashIncrementAsync($"user:{userId}", "loginCount", 1);
```

### Key Pattern Removal

```csharp
public async Task<long> RemoveByPrefixAsync(string prefix)
{
    var db = _redis.GetDatabase();
    var server = _redis.GetServer(_redis.GetEndPoints().First());
    var keysToDelete = new List<RedisKey>();

    await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
        keysToDelete.Add(key);

    if (keysToDelete.Count == 0) return 0;
    return await db.KeyDeleteAsync(keysToDelete.ToArray());
}
```

## Write-Through Pattern

```csharp
public async Task<Product> UpdateProductAsync(Product product, CancellationToken ct = default)
{
    _dbContext.Products.Update(product);
    await _dbContext.SaveChangesAsync(ct);

    var options = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    };
    await _cache.SetStringAsync($"product:{product.Id}", JsonSerializer.Serialize(product), options, ct);

    return product;
}
```

## Connection Management

- Register `IConnectionMultiplexer` as **Singleton** -- never create per request
- `ConnectionMultiplexer` is thread-safe and designed for reuse
- Set `AbortOnConnectFail = false` for resilience
- Use connection event handlers for logging failures and restores

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis"),
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "cache", "redis" });
```

## Best Practices

- Always set expiration on cache entries
- Use structured keys with namespace: `"entity:id"`, `"entity:category:id"`
- Invalidate related cache keys on writes
- Use `IDistributedCache` for simple get/set; `StackExchange.Redis` for data structures
- Use SCAN (not KEYS) for pattern-based key searches in production
- Serialize with `System.Text.Json` (prefer over Newtonsoft for new code)
