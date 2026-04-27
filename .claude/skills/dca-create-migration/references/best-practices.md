# Best Practices

Principles, versioning, team workflow, deployment, and security guidelines for FluentMigrator migrations.

---

## Migration Design Principles

### One Responsibility per Migration

Each migration should do exactly one thing: create one table, add one column, create one index.

```csharp
// CORRECT: Single responsibility
[Migration(27)]
public class M027CreatePrototypeTable : Migration
{
    private readonly string _tableName = "prototypes";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
            .InSchema(_schemaName)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("number").AsString(50).NotNullable().Unique()
            .WithColumn("issue_date").AsDateTime().NotNullable()
            .WithColumn("expiration_date").AsDateTime().NotNullable()
            .WithColumn("status").AsString(20).NotNullable();
    }

    public override void Down()
    {
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

Benefits: granular rollback, easier code review, simpler debugging, fewer merge conflicts.

**Exception**: Creating a table with its indexes in the same migration is acceptable since the indexes are part of the table's definition.

### Atomic Migrations

Migrations are transactional by default (all-or-nothing). Only use `TransactionBehavior.None` when absolutely required:

```csharp
// Non-transactional: only for operations that require it
[Migration(51, TransactionBehavior.None)]
public class M051CreateIndexConcurrently : Migration
{
    public override void Up()
    {
        // PostgreSQL: CREATE INDEX CONCURRENTLY cannot run inside a transaction
        Execute.Sql(@"
            CREATE INDEX CONCURRENTLY ix_prototypes_status
            ON public.prototypes(status);
        ");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP INDEX IF EXISTS public.ix_prototypes_status;");
    }
}
```

### Reversible Migrations

Always implement `Down()` as the exact inverse of `Up()`:

```csharp
public override void Up()
{
    Alter.Table(_tableName).InSchema(_schemaName)
        .AddColumn("company_id").AsGuid().NotNullable()
        .WithDefaultValue(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}

public override void Down()
{
    Delete.Column("company_id")
        .FromTable(_tableName).InSchema(_schemaName);
}
```

Some operations are inherently irreversible (deleting a column loses data). Document this in `Down()` and ensure backups exist before running such migrations in production.

### Independence from Application Code

Migrations must never reference domain models, enums, or application constants:

```csharp
// WRONG: References domain enum
var defaultStatus = PrototypeStatus.Draft.ToString();

// CORRECT: Uses literal constant
private const string DefaultStatus = "Draft";
```

Migrations must be immutable and execute identically regardless of application code changes.

---

## Versioning

### Sequential Numbers (APSYS Standard)

```csharp
[Migration(23)] public class M023CreateRolesTable : Migration { }
[Migration(24)] public class M024CreateUsersTable : Migration { }
[Migration(26)] public class M026TechnicalStandardsView : Migration { }  // Gap is OK
[Migration(27)] public class M027CreatePrototypeTable : Migration { }
```

Rules:
1. **Always incremental**: each new migration has a number greater than the previous
2. **Never reuse numbers**: if a migration is deleted, its number is retired
3. **Gaps are allowed**: 26, 27, 30 is valid -- no need to fill gaps
4. **Never backfill**: inserting M012 after M015 has already been applied causes execution order issues

### Timestamp Alternative

For teams with frequent branch conflicts:

```csharp
[Migration(20250114153045)] // YYYYMMDDHHmmss
public class M20250114153045_CreatePrototypeTable : Migration { }
```

Pros: no conflicts. Cons: harder to read, harder to reference in conversations.

---

## Team Workflow

### Resolving Version Conflicts

When two developers create the same migration number on separate branches:

1. The first to merge keeps the number
2. The second renumbers to the next available number before merging

### Code Review Checklist

- Naming follows `M{NNN}{Description}` convention
- Version number is sequential and not reused
- `Down()` is implemented and is the inverse of `Up()`
- No dependencies on application code
- Frequently queried columns have indexes
- No SQL injection (no external input in SQL)
- No hardcoded secrets or passwords
- Author confirmed testing Up/Down/Up locally

### Communication

Before creating a migration, communicate with the team:
- Announce the migration number you will use
- State which table(s) you will modify
- Prevents numbering conflicts and concurrent modifications to the same table

---

## Deployment

### CI/CD Pipeline

```yaml
steps:
  - name: Run Migrations Up
    run: dotnet run --project src/your.migrations -- /cnn:"${{ secrets.DB_CONNECTION }}"

  - name: Run Migrations Down (rollback test)
    run: dotnet run --project src/your.migrations -- /cnn:"${{ secrets.DB_CONNECTION }}" /action:rollback

  - name: Run Migrations Up Again (idempotency)
    run: dotnet run --project src/your.migrations -- /cnn:"${{ secrets.DB_CONNECTION }}"
```

### Pre-Migration Backup

Always back up the database before running migrations in production:

```bash
# PostgreSQL full backup
pg_dump -h $DB_HOST -U $DB_USER -d $DB_NAME -F c -f backup_$(date +%Y%m%d_%H%M%S).dump
```

### Rollback Plan

Have a rollback plan before every production deployment:

1. **Migration fails during execution**: FluentMigrator automatically rolls back (transactional)
2. **Migration succeeds but causes issues**: Run rollback action
3. **Data loss occurred**: Restore from backup

```bash
# Programmatic rollback
dotnet run --project migrations -- /cnn:"$PROD_CN" /action:rollback

# Manual rollback (last resort)
# 1. Execute the Down() SQL manually
# 2. Remove from versioninfo:
DELETE FROM public.versioninfo WHERE version = 85;
```

---

## Security

### No External Input

Migrations must never accept user input or environment-dependent values in SQL:

```csharp
// CORRECT: Hardcoded literal values only
Execute.Sql(@"
    INSERT INTO roles (id, name) VALUES
    ('11111111-1111-1111-1111-111111111111', 'Admin'),
    ('22222222-2222-2222-2222-222222222222', 'User');
");
```

### No Secrets in Code

Never hardcode passwords, API keys, or credentials in migration files. Sensitive seed data should be in external scripts that are not committed to the repository.

### Minimum Privileges

The migration runner database user should have only the permissions needed:

```sql
GRANT CONNECT ON DATABASE yourdb TO migrations_user;
GRANT USAGE, CREATE ON SCHEMA public TO migrations_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO migrations_user;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO migrations_user;
```

---

## Maintenance

### Never Delete Applied Migrations

Migrations that have been executed in any environment must remain in the repository. They are needed to recreate databases from scratch for new environments.

### Fixing Bugs in Migrations

**Before production**: Fix the migration directly.

**After production**: Create a new corrective migration:

```csharp
// M160 already applied in production with wrong length
// Create M161 to fix it
[Migration(161)]
public class M161FixEmailColumnLength : Migration
{
    public override void Up()
    {
        Alter.Table("users").InSchema("public")
            .AlterColumn("email").AsString(255).NotNullable();
    }

    public override void Down()
    {
        Alter.Table("users").InSchema("public")
            .AlterColumn("email").AsString(100).NotNullable();
    }
}
```

### Batch Processing for Large Tables

When migrating data on tables with millions of rows:

```csharp
Execute.Sql(@"
    DO $$
    DECLARE
        batch_size INT := 10000;
        rows_affected INT;
    BEGIN
        LOOP
            UPDATE public.orders
            SET normalized_status = LOWER(status)
            WHERE id IN (
                SELECT id FROM public.orders
                WHERE normalized_status IS NULL
                LIMIT batch_size
            );
            GET DIAGNOSTICS rows_affected = ROW_COUNT;
            EXIT WHEN rows_affected = 0;
            PERFORM pg_sleep(0.1);
        END LOOP;
    END $$;
");
```

This avoids locking the entire table and allows monitoring progress.

---

## Naming Conventions

### File and Class Names

Format: `M{NNN}{DescriptionInPascalCase}.cs`

```
CORRECT:
- M023CreateRolesTable.cs
- M024CreateUsersTable.cs
- M027CreatePrototypeTable.cs

WRONG:
- Migration023.cs              -- Not descriptive
- M023_create_roles_table.cs   -- Use PascalCase, not snake_case
- M23CreateRolesTable.cs       -- Missing zero-padding
- CreateRolesTable.cs          -- Missing M prefix and number
```

Class name must be identical to the file name (without `.cs`). Description should state the action: Create/Add/Alter/Delete + Target.

### Table, Index, and Key Names

**Tables**: `snake_case`, plural

```csharp
Create.Table("prototypes")          // snake_case, plural
Create.Table("technical_standards") // multi-word with underscore
Create.Table("user_roles")          // junction table
```

**Indexes**: `idx_{table}_{column(s)}`

```csharp
Create.Index("idx_prototypes_number")
Create.Index("idx_prototypes_status_issue_date")  // composite
Create.Index("idx_users_email")
```

**Foreign Keys**: `fk_{source_table}_{source_column}`

```csharp
Create.ForeignKey("fk_user_roles_user_id")
    .FromTable("user_roles").ForeignColumn("user_id")
    .ToTable("users").PrimaryColumn("id");

Create.ForeignKey("fk_user_roles_role_id")
    .FromTable("user_roles").ForeignColumn("role_id")
    .ToTable("roles").PrimaryColumn("id");
```

**Primary Keys**: `pk_{table}`

```csharp
Create.PrimaryKey("pk_user_roles")
    .OnTable("user_roles").WithSchema("public")
    .Columns("user_id", "role_id");
```

---

## Performance: Indexes

### Creating Indexes with Tables

```csharp
[Migration(60)]
public class M060CreateOrdersTable : Migration
{
    public override void Up()
    {
        Create.Table("orders").InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("order_number").AsString(50).NotNullable().Unique()
            .WithColumn("customer_id").AsGuid().NotNullable()
            .WithColumn("status").AsString(20).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("idx_orders_customer_id").OnTable("orders").OnColumn("customer_id");
        Create.Index("idx_orders_status").OnTable("orders").OnColumn("status");
        Create.Index("idx_orders_created_at").OnTable("orders").OnColumn("created_at");
    }

    public override void Down()
    {
        Delete.Table("orders").InSchema("public");
    }
}
```

### CONCURRENTLY for Large Tables (PostgreSQL)

For tables with > 1 million rows in production with active traffic:

```csharp
[Migration(61, TransactionBehavior.None)]  // CONCURRENTLY requires non-transactional
public class M061CreateIndexOnLargeTable : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_orders_customer_id_created_at
            ON public.orders(customer_id, created_at);
        ");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP INDEX IF EXISTS public.idx_orders_customer_id_created_at;");
    }
}
```

Caveats: slower than normal index creation, requires `TransactionBehavior.None`, can fail and leave an INVALID index.

---

## Performance: Batch Insert

```csharp
// SLOW: 10,000 individual transactions
for (int i = 0; i < 10000; i++)
{
    Insert.IntoTable("products").Row(new { id = Guid.NewGuid(), name = $"Product {i}" });
}

// FAST: 1 SQL statement
Execute.Sql(@"
    INSERT INTO public.products (id, name)
    SELECT gen_random_uuid(), 'Product ' || generate_series
    FROM generate_series(1, 10000);
");
```

---

## Zero-Downtime Migrations

For changes that require high availability, split into multiple migrations:

**Step 1**: Add column as nullable

```csharp
[Migration(70)]
public class M070AddFullNameStep1 : Migration
{
    public override void Up()
    {
        Alter.Table("users").AddColumn("full_name").AsString(500).Nullable();
    }
    public override void Down()
    {
        Delete.Column("full_name").FromTable("users");
    }
}
```

**Step 2**: Deploy code that populates the column

**Step 3**: Migrate existing data

```csharp
[Migration(71)]
public class M071AddFullNameStep2 : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            UPDATE public.users
            SET full_name = CONCAT(first_name, ' ', last_name)
            WHERE full_name IS NULL;
        ");
    }
    public override void Down()
    {
        Execute.Sql(@"UPDATE public.users SET full_name = NULL;");
    }
}
```

**Step 4**: Make column NOT NULL

```csharp
[Migration(72)]
public class M072AddFullNameStep3 : Migration
{
    public override void Up()
    {
        Alter.Table("users").AlterColumn("full_name").AsString(500).NotNullable();
    }
    public override void Down()
    {
        Alter.Table("users").AlterColumn("full_name").AsString(500).Nullable();
    }
}
```

---

## Testing

### Local Testing Workflow

Before every commit, test the full cycle:

```bash
# 1. Run migration Up
dotnet run --project src/your.migrations -- --cnn "Host=localhost;..." run

# 2. Verify in database
psql -h localhost -U postgres -d yourdb -c "\dt public.*"

# 3. Run rollback Down
dotnet run --project src/your.migrations -- --cnn "Host=localhost;..." rollback

# 4. Verify rollback
psql -h localhost -U postgres -d yourdb -c "\dt public.*"

# 5. Run Up again (idempotency check)
dotnet run --project src/your.migrations -- --cnn "Host=localhost;..." run
```

### CI/CD Testing (GitHub Actions)

```yaml
name: Test Migrations
on: [pull_request]

jobs:
  test-migrations:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:11
        env:
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: testdb
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Run Migrations Up
        run: dotnet run --project src/your.migrations -- --cnn "Host=localhost;Database=testdb;Username=postgres;Password=postgres" run

      - name: Run Migrations Down
        run: dotnet run --project src/your.migrations -- --cnn "Host=localhost;Database=testdb;Username=postgres;Password=postgres" rollback

      - name: Run Migrations Up Again (idempotency)
        run: dotnet run --project src/your.migrations -- --cnn "Host=localhost;Database=testdb;Username=postgres;Password=postgres" run
```

---

## Seed Data

### Separate Schema from Seed Data

Never mix schema migrations with seed data. Use separate migrations:

```csharp
// Schema migration
[Migration(100)]
public class M100CreateRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("roles")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("name").AsString().NotNullable();
    }
    public override void Down() { Delete.Table("roles"); }
}

// Seed data migration (AFTER schema)
[Migration(101)]
[Tags("SeedData")]
public class M101SeedDefaultRoles : Migration
{
    public override void Up()
    {
        Insert.IntoTable("roles")
            .Row(new { id = Guid.Parse("..."), name = "Admin" })
            .Row(new { id = Guid.Parse("..."), name = "User" });
    }
    public override void Down() { Delete.FromTable("roles").AllRows(); }
}
```

### Migration Profiles with Tags

Use Tags for selective execution by environment:

```csharp
[Migration(110)]
[Tags("Development", "SeedData")]
public class M110SeedTestUsers : Migration
{
    public override void Up()
    {
        Insert.IntoTable("users")
            .Row(new { id = Guid.NewGuid(), email = "test@example.com", name = "Test User" });
    }
    public override void Down()
    {
        Delete.FromTable("users").Row(new { email = "test@example.com" });
    }
}
```

### Reference Data

For lookup data (countries, postal codes, etc.), use `ON CONFLICT DO NOTHING` for idempotency:

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
    public override void Down() { Delete.FromTable("countries").AllRows(); }
}
```

Guidelines:
- Use fixed GUIDs for reference data
- Tag with `ReferenceData` for identification
- Consider CSV/JSON files for large volumes
