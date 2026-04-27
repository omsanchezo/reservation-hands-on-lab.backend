# Data Seeding

Patterns for inserting reference data and seed data in migrations.

---

## Pattern 21: Insert Reference Data

Use the FluentMigrator API with anonymous objects and fixed GUIDs:

```csharp
[Migration(47)]
public class M047SeedRoles : Migration
{
    private readonly string _tableName = "roles";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Insert.IntoTable(_tableName)
              .InSchema(_schemaName)
              .Row(new
              {
                  id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                  name = "Admin"
              })
              .Row(new
              {
                  id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                  name = "User"
              })
              .Row(new
              {
                  id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                  name = "Guest"
              });
    }

    public override void Down()
    {
        Delete.FromTable(_tableName)
              .InSchema(_schemaName)
              .Row(new { id = Guid.Parse("11111111-1111-1111-1111-111111111111") })
              .Row(new { id = Guid.Parse("22222222-2222-2222-2222-222222222222") })
              .Row(new { id = Guid.Parse("33333333-3333-3333-3333-333333333333") });
    }
}
```

Key points:
- Use fixed GUIDs for reference data so it is consistent across environments
- `Insert.IntoTable()` with `.Row()` for type-safe inserts
- `Down()` deletes by ID to reverse the seed precisely
- Keep schema and seed data in **separate migrations**

---

## Pattern 22: Bulk Insert with SQL

For larger data sets or database-specific functions:

```csharp
[Migration(48)]
public class M048SeedCategories : Migration
{
    private readonly string _tableName = "categories";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Execute.Sql($@"
            INSERT INTO {_schemaName}.{_tableName} (id, name, description, created_at)
            VALUES
                (gen_random_uuid(), 'Electronics', 'Electronic devices', NOW()),
                (gen_random_uuid(), 'Books', 'Books and magazines', NOW()),
                (gen_random_uuid(), 'Clothing', 'Apparel and accessories', NOW()),
                (gen_random_uuid(), 'Food', 'Food and beverages', NOW());
        ");
    }

    public override void Down()
    {
        Execute.Sql($@"
            DELETE FROM {_schemaName}.{_tableName}
            WHERE name IN ('Electronics', 'Books', 'Clothing', 'Food');
        ");
    }
}
```

Key points:
- `gen_random_uuid()` generates UUIDs in PostgreSQL (use `NEWID()` for SQL Server)
- `NOW()` for timestamps in PostgreSQL (use `GETDATE()` for SQL Server)
- `Down()` deletes by a natural key (name) since UUIDs are generated
- Prefer fixed GUIDs (Pattern 21) when exact ID values matter

---

## Idempotent Seed Data

Use `ON CONFLICT DO NOTHING` (PostgreSQL) to make inserts idempotent:

```csharp
[Migration(120)]
[Tags("ReferenceData")]
public class M120SeedCountries : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            INSERT INTO public.countries (id, code, name) VALUES
            ('11111111-1111-1111-1111-111111111111', 'MX', 'Mexico'),
            ('22222222-2222-2222-2222-222222222222', 'US', 'United States'),
            ('33333333-3333-3333-3333-333333333333', 'CA', 'Canada')
            ON CONFLICT (code) DO NOTHING;
        ");
    }

    public override void Down()
    {
        Delete.FromTable("countries").InSchema("public").AllRows();
    }
}
```

Key points:
- `ON CONFLICT DO NOTHING` prevents errors if the data already exists
- Requires a UNIQUE constraint or index on the conflict column (`code`)
- Use fixed GUIDs for reference data
- Tag with `[Tags("ReferenceData")]` for identification

---

## Separating Schema and Seed Migrations

Schema and data should always be in separate migrations:

```csharp
// Schema migration: creates the table structure
[Migration(100)]
public class M100CreateRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("roles").InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("name").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("roles").InSchema("public");
    }
}

// Seed migration: populates reference data (separate migration, later number)
[Migration(101)]
[Tags("SeedData")]
public class M101SeedDefaultRoles : Migration
{
    public override void Up()
    {
        Insert.IntoTable("roles").InSchema("public")
            .Row(new { id = Guid.Parse("11111111-1111-1111-1111-111111111111"), name = "Admin" })
            .Row(new { id = Guid.Parse("22222222-2222-2222-2222-222222222222"), name = "User" });
    }

    public override void Down()
    {
        Delete.FromTable("roles").InSchema("public").AllRows();
    }
}
```

---

## Migration Tags for Selective Execution

Use `[Tags]` to categorize seed migrations and run them selectively:

```csharp
[Migration(110)]
[Tags("Development", "SeedData")]
public class M110SeedTestUsers : Migration
{
    public override void Up()
    {
        Insert.IntoTable("users").InSchema("public")
            .Row(new {
                id = Guid.NewGuid(),
                email = "test@example.com",
                name = "Test User"
            });
    }

    public override void Down()
    {
        Delete.FromTable("users").InSchema("public")
            .Row(new { email = "test@example.com" });
    }
}
```

Configure the runner to filter by tag:

```csharp
.ConfigureRunner(rb => rb
    .AddPostgres11_0()
    .WithGlobalConnectionString(connectionString)
    .ScanIn(typeof(M001Sandbox).Assembly).For.Migrations()
    .WithMigrationsIn("Development") // Only run migrations tagged "Development"
)
```

---

## Batch Insert for Large Data Sets

For inserting thousands of rows, use SQL directly instead of the API:

```csharp
// Slow: individual Insert.IntoTable() calls
for (int i = 0; i < 10000; i++)
{
    Insert.IntoTable("products").Row(new { id = Guid.NewGuid(), name = $"Product {i}" });
}

// Fast: single SQL batch insert
Execute.Sql(@"
    INSERT INTO public.products (id, name)
    SELECT gen_random_uuid(), 'Product ' || generate_series
    FROM generate_series(1, 10000);
");
```

---

## Security Rules for Seed Data

- **Never** hardcode passwords, API keys, or secrets in migration files
- Use fixed GUIDs only for non-sensitive reference data (roles, countries, statuses)
- Sensitive seed data (admin users with passwords) should be in external scripts not committed to the repository
- All values in migrations must be literal constants -- never accept external input
