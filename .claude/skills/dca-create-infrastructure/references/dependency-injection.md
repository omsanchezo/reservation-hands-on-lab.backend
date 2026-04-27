# Dependency Injection

## Service Lifetimes

| Lifetime | Instances | When to Use | Example |
|----------|-----------|-------------|---------|
| **Singleton** | 1 per app | Stateless, expensive to create, thread-safe | DateTimeProvider, Cache |
| **Scoped** | 1 per request | Handlers, Repositories with DbContext/Session | CreateUserHandler, UserRepository |
| **Transient** | 1 per injection | Lightweight, stateless | PasswordHasher, Validators |

```csharp
// Singleton: stateless, thread-safe services
services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
services.AddSingleton<IGuidGenerator, GuidGenerator>();

// Scoped: handlers that process one request, repos that depend on ISession
services.AddScoped<CreateUserHandler>();
services.AddScoped<IUserRepository, UserRepository>();

// Transient: lightweight, stateless services
services.AddTransient<IPasswordHasher, PasswordHasher>();
services.AddTransient<ITokenGenerator, JwtTokenGenerator>();
```

## Constructor Injection (Preferred)

```csharp
// GOOD: Constructor Injection - all dependencies explicit and immutable
public class CreateUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }
}

// GOOD: Primary Constructor (C# 12+)
public class CreateUserHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<CreateUserHandler> logger)
{
    public async Task<Result<User>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        var hashedPassword = passwordHasher.HashPassword(command.Password);
        // ...
    }
}
```

```csharp
// BAD: Property Injection - dependencies can be null
public class CreateUserHandler
{
    public IUserRepository? UserRepository { get; set; }
    public IPasswordHasher? PasswordHasher { get; set; }
}
```

## Registration Patterns

### Extension Methods for Organization

```csharp
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IPasswordHasher, PasswordHasher>();
        return services;
    }

    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}

// Program.cs
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer();
```

## Scrutor Auto-Registration

```csharp
// Marker interfaces for lifetime control
public interface ITransientService { }
public interface IScopedService { }
public interface ISingletonService { }

// Register by convention
services.Scan(scan => scan
    .FromAssemblyOf<CreateUserHandler>()
    .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Handler")))
        .AsSelf()
        .WithScopedLifetime()
    .AddClasses(classes => classes.AssignableTo<ITransientService>())
        .AsImplementedInterfaces()
        .WithTransientLifetime()
    .AddClasses(classes => classes.AssignableTo<ISingletonService>())
        .AsImplementedInterfaces()
        .WithSingletonLifetime());

// Auto-register all repositories
services.Scan(scan => scan
    .FromAssemblyOf<UserRepository>()
    .AddClasses(classes => classes.AssignableTo<IRepository>())
        .AsImplementedInterfaces()
        .WithScopedLifetime());
```

## Decoration with Scrutor

```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.Decorate<IUserRepository, LoggingUserRepositoryDecorator>();
services.Decorate<IUserRepository, CachingUserRepositoryDecorator>();
// Result: Caching -> Logging -> UserRepository

public class LoggingUserRepositoryDecorator : IUserRepository
{
    private readonly IUserRepository _inner;
    private readonly ILogger<LoggingUserRepositoryDecorator> _logger;

    public LoggingUserRepositoryDecorator(IUserRepository inner, ILogger<LoggingUserRepositoryDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting user {UserId}", id);
        var user = await _inner.GetByIdAsync(id, ct);
        if (user == null) _logger.LogWarning("User {UserId} not found", id);
        return user;
    }
}
```

## Anti-Patterns

### Captive Dependency

```csharp
// BAD: Singleton captures Scoped dependency
public class SingletonService
{
    private readonly IUserRepository _userRepository; // Scoped!
    public SingletonService(IUserRepository userRepository) { _userRepository = userRepository; }
}

// GOOD: Use IServiceScopeFactory
public class SingletonService(IServiceScopeFactory scopeFactory)
{
    public async Task DoWorkAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    }
}
```

### Service Locator (Anti-Pattern)

```csharp
// BAD: hides dependencies, hard to test
public class CreateUserHandler
{
    private readonly IServiceProvider _serviceProvider;
    public async Task Handle(CreateUserCommand command, CancellationToken ct)
    {
        var userRepository = _serviceProvider.GetRequiredService<IUserRepository>();
    }
}

// GOOD: Constructor Injection
public class CreateUserHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
{
    public async Task Handle(CreateUserCommand command, CancellationToken ct) { ... }
}
```

### God Object (Too Many Dependencies)

```csharp
// BAD: 12 dependencies - violates SRP
public class OrderService(
    IUserRepository userRepository, IProductRepository productRepository,
    IOrderRepository orderRepository, IInventoryService inventoryService,
    IPaymentService paymentService, IShippingService shippingService,
    INotificationService notificationService, ITaxCalculationService taxCalculationService,
    IDiscountService discountService, ILogger<OrderService> logger,
    IEmailSender emailSender, ISmsSender smsSender) { }

// GOOD: Split into focused handlers
public class CreateOrderHandler(IUserRepository userRepository, IProductRepository productRepository,
    IOrderRepository orderRepository, IInventoryService inventoryService) { }
public class ProcessPaymentHandler(IOrderRepository orderRepository,
    IPaymentService paymentService, INotificationService notificationService) { }
```

### Registering Concrete Types

```csharp
// BAD
services.AddScoped<UserRepository>();

// GOOD
services.AddScoped<IUserRepository, UserRepository>();
```

## Checklist: Adding a New Service

- [ ] Define interface in Domain or Application layer
- [ ] Implement in Infrastructure or Application
- [ ] Use Constructor Injection for dependencies
- [ ] Determine lifetime: Stateless? Singleton/Transient. State per request? Scoped. Depends on Scoped? Scoped.
- [ ] Register in the appropriate extension method
- [ ] Verify no Captive Dependencies (Singleton -> Scoped)
- [ ] Verify no circular dependencies
- [ ] Keep dependencies per class under 5

## Checklist: Code Review

- [ ] All dependencies injected via constructor
- [ ] No Property Injection (unless justified)
- [ ] No Service Locator Pattern
- [ ] Lifetimes appropriate for each service
- [ ] No Captive Dependencies
- [ ] Singleton services are thread-safe
- [ ] Interfaces used instead of concrete types
- [ ] No `new` for services that should be injected
- [ ] Reasonable number of dependencies per class (<5)
