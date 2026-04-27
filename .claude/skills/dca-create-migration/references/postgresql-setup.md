# PostgreSQL Setup

## Overview

Configures PostgreSQL as the project database: Npgsql driver, NHibernate dialect/driver, connection string builder, and environment variables.

**Requires:** Infrastructure layer already set up.

## NuGet Package

```bash
dotnet add src/{ProjectName}.infrastructure/{ProjectName}.infrastructure.csproj package Npgsql
```

## Environment Variables

Add to `.env`:

```env
# PostgreSQL
DB_HOST=localhost
DB_PORT=5432
DB_NAME={projectname}-db
DB_USER=postgres
DB_PASSWORD=your_password
```

## Steps

### 1. Add ConnectionStringBuilder

Copy the `ConnectionStringBuilder.cs` template to `src/{ProjectName}.infrastructure/nhibernate/`.

### 2. Configure NHSessionFactory

In `NHSessionFactory.cs`, set the PostgreSQL driver and dialect:

```csharp
cfg.DataBaseIntegration(c =>
{
    c.Driver<NpgsqlDriver>();
    c.Dialect<PostgreSQL83Dialect>();
    c.ConnectionString = this.ConnectionString;
    c.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
});
```

Required imports:

```csharp
using NHibernate.Driver;
using NHibernate.Dialect;
```

### 3. Configure DI in WebApi

In `src/{ProjectName}.webapi/infrastructure/NHibernateServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection ConfigureNHibernate(
    this IServiceCollection services)
{
    var connectionString = ConnectionStringBuilder.Build();

    services.AddSingleton(new NHSessionFactory(connectionString));
    services.AddScoped(sp =>
        sp.GetRequiredService<NHSessionFactory>().BuildNHibernateSessionFactory().OpenSession());

    return services;
}
```

## Verification

```bash
# Verify PostgreSQL is running
psql -h localhost -U postgres -c "SELECT version();"

# Verify .NET connection
dotnet build
dotnet run --project src/{ProjectName}.webapi
```

## Troubleshooting

**`Npgsql.NpgsqlException: Connection refused`**
- Verify PostgreSQL is running
- Check host and port in environment variables

**`password authentication failed`**
- Verify credentials in `.env`
- Verify the user exists in PostgreSQL
