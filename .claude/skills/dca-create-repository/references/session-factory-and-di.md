# Session Factory and DI Reference

Complete reference for NHSessionFactory configuration and Dependency Injection setup in APSYS backend projects.

---

## NHSessionFactory (Full Code)

```csharp
using {project}.infrastructure.nhibernate.mappers;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;

namespace {project}.infrastructure.nhibernate;

public class NHSessionFactory(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public ISessionFactory BuildNHibernateSessionFactory()
    {
        // 1. Create ModelMapper
        var mapper = new ModelMapper();

        // 2. Auto-register all ClassMapping<T> in the assembly
        mapper.AddMappings(typeof(RoleMapper).Assembly.ExportedTypes);

        // 3. Compile mappings
        HbmMapping domainMapping = mapper.CompileMappingForAllExplicitlyAddedEntities();

        // 4. Configure NHibernate
        var cfg = new Configuration();
        cfg.DataBaseIntegration(c =>
        {
            c.Driver<NpgsqlDriver>();
            c.Dialect<PostgreSQL83Dialect>();
            c.ConnectionString = this.ConnectionString;
            c.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
        });

        // 5. Add compiled mappings
        cfg.AddMapping(domainMapping);

        // 6. Build SessionFactory
        return cfg.BuildSessionFactory();
    }
}
```

### Auto-Registration of Mappers

```csharp
mapper.AddMappings(typeof(RoleMapper).Assembly.ExportedTypes);
```

This automatically discovers and registers all `public` classes inheriting `ClassMapping<T>` in the same assembly as `RoleMapper`. New mappers are detected automatically -- no manual registration needed.

---

## SQL Server Variant

```csharp
cfg.DataBaseIntegration(c =>
{
    c.Driver<MicrosoftDataSqlClientDriver>();
    c.Dialect<MsSql2012Dialect>();
    c.ConnectionString = this.ConnectionString;
    c.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
});
```

---

## DI Configuration (ServiceCollectionExtender)

```csharp
using {project}.domain.interfaces.repositories;
using {project}.infrastructure.nhibernate;

namespace {project}.webapi.infrastructure;

public static class ServiceCollectionExtender
{
    public static IServiceCollection ConfigureUnitOfWork(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Build connection string from environment variables
        string connectionString = ConnectionStringBuilder.BuildPostgresConnectionString();

        // 2. Create NHSessionFactory
        var factory = new NHSessionFactory(connectionString);
        var sessionFactory = factory.BuildNHibernateSessionFactory();

        // 3. SessionFactory: Singleton (thread-safe, expensive to create)
        services.AddSingleton(sessionFactory);

        // 4. ISession: Scoped (one per HTTP request)
        services.AddScoped(factory => sessionFactory.OpenSession());

        // 5. IUnitOfWork: Scoped (wraps ISession, same lifetime)
        services.AddScoped<IUnitOfWork, NHUnitOfWork>();

        return services;
    }
}
```

---

## DI Lifetime Table

| Component | Lifetime | Reason |
|-----------|----------|--------|
| `ISessionFactory` | **Singleton** | Thread-safe, expensive to create, immutable |
| `ISession` | **Scoped** | One per HTTP request, NOT thread-safe |
| `IUnitOfWork` | **Scoped** | Wraps ISession, must have same lifetime |
| Repositories | **Transient** | Created on-demand by UnitOfWork (lazy properties) |
| Validators | **Scoped** | Resolved by repositories via IServiceProvider |

---

## ConfigureValidators

```csharp
public static IServiceCollection ConfigureValidators(this IServiceCollection services)
{
    services.AddScoped<AbstractValidator<User>, UserValidator>();
    services.AddScoped<AbstractValidator<Role>, RoleValidator>();
    services.AddScoped<AbstractValidator<Prototype>, PrototypeValidator>();
    services.AddScoped<AbstractValidator<TechnicalStandard>, TechnicalStandardValidator>();
    // Add one line per entity with a validator

    return services;
}
```

**Important**: Every entity that has a CRUD repository must have its validator registered here. If missing, the `NHRepository` constructor will throw `InvalidOperationException`.

---

## ConnectionStringBuilder

```csharp
public static class ConnectionStringBuilder
{
    public static string BuildPostgresConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "mydb";
        var username = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD");

        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("DB_PASSWORD environment variable is required");

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
```

---

## Program.cs Wiring

```csharp
using FastEndpoints;
using {project}.webapi.infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureUnitOfWork(configuration)    // SessionFactory + Session + UnitOfWork
    .ConfigureValidators()                  // AbstractValidator<T> registrations
    .AddFastEndpoints();
    // ... other services

var app = builder.Build();

app.UseFastEndpoints();
// ... other middleware

await app.RunAsync();
```

---

## DI Flow Per HTTP Request

```
HTTP Request arrives
    |
ASP.NET Core creates new Scope
    |
    +-- SessionFactory (Singleton, already exists)
    |       |
    |       +-- OpenSession() -> ISession (Scoped, new per request)
    |               |
    |               +-- new NHUnitOfWork(session, serviceProvider) (Scoped)
    |                       |
    |                       +-- Injected into Handler
    |
Handler executes business logic
    |
Scope.Dispose() -> UnitOfWork.Dispose() -> Session.Dispose()
    |
HTTP Response sent
```
