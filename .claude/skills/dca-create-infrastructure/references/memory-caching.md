# In-Memory Cache (IMemoryCache)

## When to Use

- Single server application (not web farm)
- Small to medium datasets (catalogs, configuration)
- Speed is critical (nanosecond access)
- Data can be regenerated easily
- No need to share cache between instances

Do NOT use when: web farm, multiple instances, cache must survive restarts.

## Configuration

### Basic

```csharp
// Program.cs
builder.Services.AddMemoryCache();
```

### Advanced (with size limits)

```csharp
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;                                  // Abstract size units
    options.CompactionPercentage = 0.25;                       // Remove 25% on limit
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // Default: 1 min
});
```

### DI Injection

```csharp
public class ProductService
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;

    public ProductService(IMemoryCache cache, AppDbContext dbContext)
    {
        _cache = cache;
        _dbContext = dbContext;
    }
}
```

## Usage Patterns

### TryGetValue (Manual Control)

```csharp
public async Task<Product> GetProductAsync(int id)
{
    var cacheKey = $"product:{id}";

    if (_cache.TryGetValue(cacheKey, out Product cachedProduct))
        return cachedProduct;

    var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    if (product != null)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(10)
        };
        _cache.Set(cacheKey, product, cacheOptions);
    }

    return product;
}
```

### GetOrCreateAsync (Recommended)

```csharp
public async Task<List<Category>> GetCategoriesAsync()
{
    return await _cache.GetOrCreateAsync("categories:all", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
        entry.SlidingExpiration = TimeSpan.FromMinutes(30);
        entry.Priority = CacheItemPriority.Normal;
        entry.Size = 1; // Required if SizeLimit is configured

        return await _dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
    });
}
```

### GetOrCreate (Synchronous)

```csharp
public List<Country> GetCountries()
{
    return _cache.GetOrCreate("countries:all", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
        entry.Priority = CacheItemPriority.High;

        return _dbContext.Countries.AsNoTracking().OrderBy(c => c.Name).ToList();
    });
}
```

## Expiration Options

### Absolute Expiration

Expires after a fixed time, regardless of access.

```csharp
entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
```

### Sliding Expiration

Expires if NOT accessed during the period. Each access renews.

```csharp
entry.SlidingExpiration = TimeSpan.FromMinutes(15);
```

### Combined (Recommended)

```csharp
entry.SlidingExpiration = TimeSpan.FromMinutes(5);              // Renews on access
entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30); // Hard maximum
```

### Recommended Expiration Times

| Data Type       | Absolute | Sliding | Example                    |
|-----------------|----------|---------|----------------------------|
| Catalogs        | 12-24h   | -       | Countries, Categories      |
| Configuration   | 30min    | 10min   | AppSettings, Features      |
| Active User     | 1h       | 15min   | UserProfile, Preferences   |
| Prices          | 5min     | -       | ProductPrices              |
| Statistics      | 10min    | -       | Dashboard, Reports         |
| Search Results  | -        | 5min    | SearchResults              |

## Size and Priority Management

```csharp
// Priority levels (eviction order: Low first, NeverRemove last)
entry.Priority = CacheItemPriority.High;
entry.Size = 10; // Required when SizeLimit is set

// Sizing helper
public static class CacheSizes
{
    public const int Tiny = 1;      // Configs, flags
    public const int Small = 5;     // Single entities
    public const int Medium = 10;   // Small lists (< 100 items)
    public const int Large = 50;    // Large lists (100-1000 items)
    public const int XLarge = 100;  // Massive datasets (> 1000 items)
}
```

## Cache Wrapper Service

```csharp
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);
    void Remove(string key);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        if (_cache.TryGetValue(key, out T cachedValue))
            return cachedValue;

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out cachedValue)) // Double-check
                return cachedValue;

            var value = await factory();
            var options = new MemoryCacheEntryOptions();
            if (absoluteExpiration.HasValue) options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
            if (slidingExpiration.HasValue) options.SlidingExpiration = slidingExpiration.Value;
            _cache.Set(key, value, options);
            return value;
        }
        finally { semaphore.Release(); }
    }

    public void Remove(string key) => _cache.Remove(key);
}
```

## Cache Invalidation

```csharp
public async Task<Product> UpdateProductAsync(Product product)
{
    _dbContext.Products.Update(product);
    await _dbContext.SaveChangesAsync();

    _cache.Remove(CacheKeys.Product(product.Id));
    _cache.Remove(CacheKeys.AllProducts());
    _cache.Remove(CacheKeys.ProductsByCategory(product.CategoryId));

    return product;
}
```

## Structured Cache Keys

```csharp
public static class CacheKeys
{
    public static string Product(int id) => $"product:{id}";
    public static string ProductsByCategory(int categoryId) => $"products:category:{categoryId}";
    public static string AllProducts() => "products:all";
}
```

## PostEviction Callbacks

```csharp
entry.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
{
    EvictionCallback = (key, value, reason, state) =>
    {
        _logger.LogInformation("Cache entry {CacheKey} evicted. Reason: {Reason}", key, reason);
    }
});

// EvictionReason: None, Removed, Replaced, Expired, TokenExpired, Capacity
```

## Anti-Patterns

- **No expiration**: Always set expiration -- `_cache.Set("key", value)` causes memory leaks
- **Caching large datasets**: Only cache small/medium datasets; paginate large ones
- **Not invalidating on write**: Always remove related keys after DB updates
- **Missing user isolation**: Include userId in keys -- `$"cart:user:{userId}"` not `"cart"`
- **Cache as lock mechanism**: Use `SemaphoreSlim` for locking, not cache entries

## Testing

```csharp
public class ProductServiceTests
{
    private readonly IMemoryCache _cache;

    public ProductServiceTests()
    {
        var options = Options.Create(new MemoryCacheOptions());
        _cache = new MemoryCache(options);
    }

    [Fact]
    public async Task GetProductAsync_CachesResult()
    {
        var result1 = await _sut.GetProductAsync(1);
        var result2 = await _sut.GetProductAsync(1);
        Assert.Same(result1, result2); // Same instance = cached
    }
}
```

## Common Errors

**`InvalidOperationException: Cache entry must specify a size when SizeLimit is set.`**
Always set `entry.Size` when `SizeLimit` is configured.

**`ObjectDisposedException: Cannot access a disposed object.`**
Ensure `IMemoryCache` is registered as Singleton (default behavior).
