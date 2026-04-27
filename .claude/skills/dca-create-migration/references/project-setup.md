# Migration Project Setup

Complete setup guide for creating a FluentMigrator console project in an APSYS backend solution.

---

## Project Structure

```
src/{ProjectName}.migrations/
├── {ProjectName}.migrations.csproj
├── Program.cs                      <- Migration runner with CLI
├── CommandLineArgs.cs              <- CLI argument parser
├── CustomVersionTableMetaData.cs   <- Version tracking config
└── M001Sandbox.cs                  <- First migration (creates unaccent extension)
```

---

## Step 1: Create Console Project

```bash
cd src
dotnet new console -n {ProjectName}.migrations
dotnet sln ../{ProjectName}.sln add {ProjectName}.migrations/{ProjectName}.migrations.csproj
```

## Step 2: Configure .csproj

Replace the contents of `src/{ProjectName}.migrations/{ProjectName}.migrations.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentMigrator" />
    <PackageReference Include="FluentMigrator.Runner" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Spectre.Console" />
    <!-- Database driver - choose one -->
    <PackageReference Include="Npgsql" />                    <!-- PostgreSQL -->
    <!-- <PackageReference Include="Microsoft.Data.SqlClient" /> --> <!-- SQL Server -->
  </ItemGroup>
</Project>
```

Required package versions in `Directory.Packages.props`:

```xml
<PackageVersion Include="FluentMigrator" Version="7.1.0" />
<PackageVersion Include="FluentMigrator.Runner" Version="7.1.0" />
<PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.7" />
<PackageVersion Include="Spectre.Console" Version="0.50.0" />
```

## Step 3: Add Template Files

### Program.cs

Migration runner with CLI support for `run` and `rollback` actions:

```csharp
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using {ProjectName}.migrations;

const string _run = "run";
const string _rollback = "rollback";

try
{
    // Read the command line parameters
    AnsiConsole.MarkupLine("Reading command line parameters...");
    CommandLineArgs parameter = [];
    if (!parameter.TryGetValue("cnn", out string? value))
        throw new ArgumentException("No [cnn] parameter received. You need pass the connection string in order to execute the migrations");

    // Create the service provider
    AnsiConsole.MarkupLine("[bold yellow]Connecting to database...[/]");
    string connectionStringValue = value;
    var serviceProvider = CreateServices(connectionStringValue);
    using var scope = serviceProvider.CreateScope();
    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

    // Check if the action is passed in the command line. If not, default to run
    if (!parameter.TryGetValue("action", out string? action) && string.IsNullOrEmpty(action))
        action = _run;

    // Execute the requested action
    if (action == _run)
    {
        AnsiConsole.Status()
            .Start("Start running migrations...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                ctx.Status("Running migrations...");
                UpdateDatabase(scope.ServiceProvider);
                runner.MigrateUp();
            });
        AnsiConsole.MarkupLine("All migrations are updated");
    }
    else if (action == _rollback)
    {
        AnsiConsole.Status()
            .Start("Start rolling back the last migration...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("blue"));
                ctx.Status("Rolling back migration...");
                var lastMigration = runner.MigrationLoader.LoadMigrations().LastOrDefault();
                var rollBackToVersion = lastMigration.Value.Version - 1;
                runner.MigrateDown(rollBackToVersion);
            });
        AnsiConsole.MarkupLine("Last transaction rolled back");

    }
    else
    {
        throw new ArgumentException("Invalid action. Please use 'run' or 'rollback'");
    }
    return 0;
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return (int)ExitCode.UnknownError;
}

/// <summary>
/// Configure the dependency injection services
/// </sumamry>
static IServiceProvider CreateServices(string? connectionString)
{
    return new ServiceCollection()
        .AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            // ============================================================
            // CONFIGURE DATABASE PROVIDER
            // See: stacks/database/{postgresql|sqlserver}/guides/setup.md
            // ============================================================

            // For PostgreSQL:
            .AddPostgres11_0()

            // For SQL Server:
            // .AddSqlServer()

            .WithGlobalConnectionString(connectionString)
            .ScanIn(typeof(M001Sandbox).Assembly).For.Migrations())
        .AddLogging(lb => lb.AddFluentMigratorConsole())
        .BuildServiceProvider(false);
}

static void UpdateDatabase(IServiceProvider serviceProvider)
{
    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}
```

### CommandLineArgs.cs

CLI argument parser that extracts `/key:value` parameters:

```csharp
using System.Text.RegularExpressions;

namespace {ProjectName}.migrations;

internal class CommandLineArgs : Dictionary<string, string>
{
    private const string Pattern = @"\/(?<argname>\w+):(?<argvalue>.+)";
    private readonly Regex _regex = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Determine if the user pass at least one valid parameter
    /// </summary>
    /// <returns></returns>
    public bool ContainsValidArguments()
    {
        return (this.ContainsKey("cnn"));
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public CommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var groups in args.Select(arg => _regex.Match(arg)).Where(m => m.Success).Select(match => match.Groups))
            this.Add(groups["argname"].Value, groups["argvalue"].Value);
    }
}

internal enum ExitCode
{
    Success = 0,
    UnknownError = 1
}
```

### CustomVersionTableMetaData.cs

Configures the table FluentMigrator uses to track applied migrations:

```csharp
using FluentMigrator.Runner.VersionTableInfo;

namespace {ProjectName}.migrations;

/// <summary>
/// Configures the table that FluentMigrator uses to track applied migrations.
/// </summary>
public class CustomVersionTableMetaData : IVersionTableMetaData
{
    public static string SchemaNameValue => "public";

    public required object ApplicationContext { get; set; }

    /// <summary>
    /// Whether FluentMigrator should create the schema if it doesn't exist.
    /// </summary>
    public bool OwnsSchema => true;

    /// <summary>
    /// Schema name where the version table will be created.
    /// </summary>
    public string SchemaName => SchemaNameValue;

    /// <summary>
    /// Name of the table that tracks applied migrations.
    /// </summary>
    public string TableName => "versioninfo";

    /// <summary>
    /// Name of the column that stores the version number.
    /// </summary>
    public string ColumnName => "version";

    /// <summary>
    /// Name of the unique index on the version column.
    /// </summary>
    public string UniqueIndexName => "uc_version";

    /// <summary>
    /// Name of the column that stores the application date.
    /// </summary>
    public string AppliedOnColumnName => "appliedon";

    /// <summary>
    /// Name of the column that stores the migration description.
    /// </summary>
    public string DescriptionColumnName => "description";

    /// <summary>
    /// Whether the table should have a primary key.
    /// false = only unique index (more efficient for this use case)
    /// </summary>
    public bool CreateWithPrimaryKey => false;
}
```

### M001Sandbox.cs

First migration that creates the PostgreSQL `unaccent` extension:

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

[Migration(1)]
public class M001Sandbox : Migration
{
    public override void Down()
    {
        Execute.Sql("DROP EXTENSION IF EXISTS unaccent;");
    }

    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
    }
}
```

### CreateDomainEventsTable.cs (Reference Template)

Complex migration example showing table creation with multiple indexes:

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

/// <summary>
/// Migration to create the domain_events table for Event Sourcing Lite pattern.
/// This table serves dual purposes:
/// 1. Outbox Pattern: Events marked with should_publish=true are processed by a background service
/// 2. Audit Trail: All events are persisted for historical and compliance purposes
/// </summary>
[Migration({MigrationNumber})]
public class M{MigrationNumber}CreateDomainEventsTable : Migration
{
    private readonly string _tableName = "domain_events";
    private readonly string _schemaName = "{SchemaName}";

    /// <summary>
    /// Creates the domain_events table with all required columns and indexes.
    /// </summary>
    public override void Up()
    {
        Create.Table(_tableName)
            .InSchema(_schemaName)
            // Identification
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("organization_id").AsGuid().NotNullable()
            // Aggregate Information
            .WithColumn("aggregate_type").AsString(200).NotNullable()
            .WithColumn("aggregate_id").AsGuid().NotNullable()
            // Event Data
            .WithColumn("event_type").AsString(200).NotNullable()
            .WithColumn("event_data").AsCustom("JSONB").NotNullable()
            .WithColumn("occurred_at").AsDateTime().NotNullable()
            // Audit Context
            .WithColumn("user_id").AsGuid().Nullable()
            .WithColumn("user_name").AsString(200).Nullable()
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("correlation_id").AsString(100).Nullable()
            .WithColumn("conversation_id").AsGuid().Nullable()
            // Outbox Pattern Control
            .WithColumn("should_publish").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("published_at").AsDateTime().Nullable()
            .WithColumn("publish_attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("last_publish_error").AsCustom("TEXT").Nullable()
            // Metadata
            .WithColumn("version").AsInt32().NotNullable().WithDefaultValue(1);

        // Indexes
        Create.Index("ix_domain_events_organization")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("organization_id").Ascending()
            .OnColumn("occurred_at").Descending();

        Create.Index("ix_domain_events_aggregate")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("aggregate_id").Ascending()
            .OnColumn("occurred_at").Descending();

        Create.Index("ix_domain_events_correlation")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("correlation_id");

        Create.Index("ix_domain_events_outbox")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("should_publish").Ascending()
            .OnColumn("published_at").Ascending()
            .OnColumn("publish_attempts").Ascending()
            .OnColumn("occurred_at").Ascending();
    }

    /// <summary>
    /// Drops the domain_events table and its indexes.
    /// </summary>
    public override void Down()
    {
        Delete.Index("ix_domain_events_outbox").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_correlation").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_aggregate").OnTable(_tableName).InSchema(_schemaName);
        Delete.Index("ix_domain_events_organization").OnTable(_tableName).InSchema(_schemaName);
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

---

## Step 4: Configure Database Provider

In `Program.cs`, uncomment the appropriate database provider:

**PostgreSQL:**
```csharp
.ConfigureRunner(rb => rb
    .AddPostgres11_0()
    .WithGlobalConnectionString(connectionString)
    .ScanIn(typeof(M001Sandbox).Assembly).For.Migrations())
```

**SQL Server:**
```csharp
.ConfigureRunner(rb => rb
    .AddSqlServer()
    .WithGlobalConnectionString(connectionString)
    .ScanIn(typeof(M001Sandbox).Assembly).For.Migrations())
```

## Step 5: Replace Namespaces

In all copied files, replace `{ProjectName}` with the actual project name.

---

## Running Migrations

### Apply All Pending Migrations

```bash
cd src/{ProjectName}.migrations
dotnet run /cnn:"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass"
```

### Rollback Last Migration

```bash
dotnet run /cnn:"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass" /action:rollback
```

### Using Environment Variables

```bash
source ../{ProjectName}.webapi/.env
dotnet run /cnn:"Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
```

---

## Creating a New Migration

1. Determine the next version number (check existing `M*.cs` files in the project)
2. Create file `M{NNN}{Description}.cs` in the project root:

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

[Migration(2)]
public class M002CreateUsersTable : Migration
{
    private const string TableName = "users";
    private const string SchemaName = "public";

    public override void Up()
    {
        Create.Table(TableName)
            .InSchema(SchemaName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("email").AsString(255).NotNullable().Unique()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table(TableName).InSchema(SchemaName);
    }
}
```

---

## Verification

```bash
# Build the project
dotnet build src/{ProjectName}.migrations

# Run migrations
dotnet run --project src/{ProjectName}.migrations /cnn:"..."

# Verify in database
SELECT * FROM public.versioninfo ORDER BY version;
```
