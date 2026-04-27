---
name: create-migration
description: >-
  Guides creation of FluentMigrator database migrations in APSYS .NET backend
  projects. Covers table creation, column alterations, indexes, foreign keys,
  views, raw SQL, and data seeding. Use when user asks to "create a migration",
  "add a table", "alter a column", "add a column", "create an index",
  "add a foreign key", "create a view", "seed data", "rename a table",
  "delete a column", or "write a database migration" in Clean Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with FluentMigrator,
  PostgreSQL or SQL Server. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 2.0.0
---

# FluentMigrator Migrations Skill

Guide for creating and maintaining FluentMigrator database migrations in APSYS .NET backend projects:
table creation, schema alterations, indexes, foreign keys, views, data seeding, and migration validation.

## Instructions

### Step 1: Identify the Operation Type

Use the decision tree to determine which pattern to follow:

```
What database change are you making?
|
+-- CREATE TABLE (new entity)
|   -> Create.Table() with columns, PK, constraints
|   -> See references/create-table-patterns.md
|
+-- ALTER TABLE (modify existing)
|   -> Add/alter/rename/delete columns
|   -> See references/alter-table-patterns.md
|
+-- CREATE INDEX
|   -> Simple, unique, composite, or filtered
|   -> See references/indexes-and-foreign-keys.md
|
+-- ADD FOREIGN KEY
|   -> Simple FK, cascade, or junction table
|   -> See references/indexes-and-foreign-keys.md
|
+-- CREATE VIEW
|   -> Simple view or view with JOINs
|   -> See references/views-and-raw-sql.md
|
+-- RAW SQL (constraint, function, extension)
|   -> Execute.Sql() for non-API operations
|   -> See references/views-and-raw-sql.md
|
+-- DATA SEEDING
    -> Reference data or bulk insert
    -> See references/data-seeding.md
```

### Step 2: Follow the Migration Pattern

Every migration follows the same base structure:

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

[Migration({N})]
public class M{NNN}{Description} : Migration
{
    private readonly string _tableName = "{table_name}";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        // Apply the change
    }

    public override void Down()
    {
        // Reverse the change (exact inverse of Up)
    }
}
```

1. Determine the next migration number (check existing `M*.cs` files)
2. Create file `M{NNN}{Description}.cs` in the migrations project root
3. Implement `Up()` using the appropriate pattern from references
4. Implement `Down()` as the exact inverse of `Up()`

### Step 3: Validate with Checklist

Use the operation-specific checklists at the end of this file to verify the generated code before finishing.

### Step 4: Validate the Migration

Every migration must pass the Up/Down/Up cycle. See `references/migration-validation.md` for the full validation process and CI/CD pipeline configuration.

```bash
dotnet run -- /cnn:"Host=localhost;..."
dotnet run -- /cnn:"Host=localhost;..." /action:rollback
dotnet run -- /cnn:"Host=localhost;..."
```

---

## References

For complete patterns, additional examples, and edge cases, consult files in `references/`:

| Topic | Reference |
|-------|-----------|
| Project Setup & Templates | `references/project-setup.md` |
| Create Table Patterns | `references/create-table-patterns.md` |
| Alter Table Patterns | `references/alter-table-patterns.md` |
| Indexes & Foreign Keys | `references/indexes-and-foreign-keys.md` |
| Views & Raw SQL | `references/views-and-raw-sql.md` |
| Data Seeding | `references/data-seeding.md` |
| Best Practices | `references/best-practices.md` |
| Migration Validation | `references/migration-validation.md` |

---

## Migration Project Structure

```
src/{ProjectName}.migrations/
├── {ProjectName}.migrations.csproj
├── Program.cs                      <- Migration runner with CLI
├── CommandLineArgs.cs              <- CLI argument parser
├── CustomVersionTableMetaData.cs   <- Version tracking config
├── M001Sandbox.cs                  <- First migration (unaccent extension)
├── M002CreateUsersTable.cs
├── M003CreateRolesTable.cs
├── M004CreateUserInRolesTable.cs
├── M005SeedDefaultRoles.cs
└── ...
```

---

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| File | `M{NNN}{Description}.cs` | `M023CreateRolesTable.cs` |
| Class | `M{NNN}{Description}` | `M023CreateRolesTable` |
| Attribute | `[Migration(N)]` | `[Migration(23)]` |
| Table | `snake_case`, plural | `users`, `user_roles` |
| Index | `ix_{table}_{columns}` | `ix_users_email` |
| Foreign Key | `fk_{from_table}_{column}` | `fk_orders_user_id` |
| Primary Key | `pk_{table}` | `pk_user_in_roles` |

---

## Design Rules

1. **One responsibility per migration**: One table, one column change, or one index per migration. Exception: a table with its indexes in the same migration.
2. **Always implement Down()**: `Down()` must be the exact inverse of `Up()`. Document irreversible operations.
3. **No application code dependencies**: Use literal constants, never reference domain models or enums.
4. **Never modify applied migrations**: If a migration has been executed in any environment, create a new corrective migration instead.
5. **Never reuse version numbers**: Deleted migration numbers are permanently retired. Gaps in numbering are allowed.
6. **Transactional by default**: Only use `TransactionBehavior.None` when required (e.g., `CREATE INDEX CONCURRENTLY`).

---

## Column Types Quick Reference

| FluentMigrator | PostgreSQL | Use For |
|----------------|------------|---------|
| `.AsGuid()` | UUID | Primary keys, foreign keys |
| `.AsString()` | TEXT | Unlimited text |
| `.AsString(N)` | VARCHAR(N) | Constrained text |
| `.AsInt32()` | INTEGER | Whole numbers |
| `.AsInt64()` | BIGINT | Large whole numbers |
| `.AsDecimal(18,2)` | NUMERIC(18,2) | Monetary values |
| `.AsBoolean()` | BOOLEAN | True/false flags |
| `.AsDateTime()` | TIMESTAMP | Date and time |
| `.AsDateTimeOffset()` | TIMESTAMPTZ | Date/time with timezone |
| `.AsDouble()` | DOUBLE PRECISION | Approximate decimals |
| `.AsCustom("JSONB")` | JSONB | JSON documents |

Default values: `WithDefaultValue(0)` for static, `WithDefault(SystemMethods.CurrentDateTime)` for dynamic.

---

## Checklists

### Create Table

- [ ] Inherits `Migration`, has `[Migration(N)]` attribute
- [ ] GUID primary key: `.WithColumn("id").AsGuid().PrimaryKey()`
- [ ] Table name is `snake_case` and plural
- [ ] All column types and constraints are specified
- [ ] Indexes created for frequently queried columns (FK columns, status, dates)
- [ ] `Down()` drops the table: `Delete.Table().InSchema()`
- [ ] Table and schema names stored in `private readonly string` fields

### Alter Table

- [ ] New columns on existing tables are `.Nullable()` unless using the three-step pattern
- [ ] `Down()` reverses each change individually
- [ ] For NOT NULL additions, uses nullable -> populate -> NOT NULL pattern
- [ ] Column type changes preserve existing data
- [ ] Rename operations are paired with application code updates

### Create Index

- [ ] Index name follows `ix_{table}_{columns}` convention
- [ ] Column order matches the most common query pattern
- [ ] Unique indexes use `.WithOptions().Unique()`
- [ ] Large table indexes use `CREATE INDEX CONCURRENTLY` with `TransactionBehavior.None`
- [ ] `Down()` drops the index by name

### Foreign Key

- [ ] FK name follows `fk_{from_table}_{column}` convention
- [ ] Referenced table exists (created in a previous migration)
- [ ] Delete rule is appropriate (`Cascade`, `SetNull`, or `NoAction`)
- [ ] FK column has an index for join performance
- [ ] `Down()` drops the FK by name

### Create View

- [ ] Uses `CREATE OR REPLACE VIEW` via `Execute.Sql()`
- [ ] Schema-qualified table references in the SQL
- [ ] Column aliases disambiguate columns from different tables
- [ ] `Down()` uses `DROP VIEW IF EXISTS`

### Data Seeding

- [ ] Schema and seed data are in separate migrations
- [ ] Reference data uses fixed GUIDs
- [ ] No hardcoded passwords or secrets
- [ ] `Down()` deletes the seeded rows (by ID or natural key)
- [ ] Uses `ON CONFLICT DO NOTHING` for idempotency where applicable

---

## Examples

### Example 1: Create a table for inspection reports

User says: "Create a migration for the inspection_reports table with id, report_number, inspector_id, status, and created_at"

Actions:
1. Check existing migrations to determine next version number (e.g., M028)
2. Create `M028CreateInspectionReportsTable.cs` with `Create.Table("inspection_reports")`, GUID PK, columns with appropriate types and constraints
3. Add indexes on `inspector_id` and `status` columns
4. Implement `Down()` with `Delete.Table()`

Result:

```csharp
[Migration(28)]
public class M028CreateInspectionReportsTable : Migration
{
    private readonly string _tableName = "inspection_reports";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
            .InSchema(_schemaName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("report_number").AsString(50).NotNullable().Unique()
            .WithColumn("inspector_id").AsGuid().NotNullable()
            .WithColumn("status").AsString(20).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);

        Create.Index("ix_inspection_reports_inspector_id")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("inspector_id");

        Create.Index("ix_inspection_reports_status")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("status");
    }

    public override void Down()
    {
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

### Example 2: Add a column with data migration

User says: "Add a full_name column to users, computed from first_name and last_name, and make it NOT NULL"

Actions:
1. Use the three-step pattern (nullable -> populate -> NOT NULL)
2. Create `M029AddFullNameToUsers.cs`
3. Step 1: Add column as nullable
4. Step 2: Populate with `CONCAT(first_name, ' ', last_name)`
5. Step 3: Alter to NOT NULL

Result:

```csharp
[Migration(29)]
public class M029AddFullNameToUsers : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Alter.Table(_tableName).InSchema(_schemaName)
            .AddColumn("full_name").AsString(500).Nullable();

        Execute.Sql($@"
            UPDATE {_schemaName}.{_tableName}
            SET full_name = CONCAT(first_name, ' ', last_name)
            WHERE full_name IS NULL;
        ");

        Alter.Table(_tableName).InSchema(_schemaName)
            .AlterColumn("full_name").AsString(500).NotNullable();
    }

    public override void Down()
    {
        Delete.Column("full_name")
            .FromTable(_tableName).InSchema(_schemaName);
    }
}
```

### Example 3: Create a view joining two tables

User says: "Create a view that shows orders with user information"

Actions:
1. Create `M030CreateOrdersWithUserView.cs`
2. Use `Execute.Sql()` with `CREATE OR REPLACE VIEW`
3. Join orders with users, alias columns to avoid ambiguity
4. `Down()` drops the view

Result:

```csharp
[Migration(30)]
public class M030CreateOrdersWithUserView : Migration
{
    private readonly string _viewName = "orders_with_user_view";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        var fullViewName = $"{_schemaName}.{_viewName}";
        Execute.Sql($@"
            CREATE OR REPLACE VIEW {fullViewName} AS
            SELECT
                o.id AS order_id,
                o.order_number,
                o.total_amount,
                o.status,
                o.created_at,
                u.id AS user_id,
                u.name AS user_name,
                u.email AS user_email
            FROM {_schemaName}.orders o
            INNER JOIN {_schemaName}.users u ON o.user_id = u.id;
        ");
    }

    public override void Down()
    {
        Execute.Sql($"DROP VIEW IF EXISTS {_schemaName}.{_viewName};");
    }
}
```

---

## Troubleshooting

### Migration not applied (skipped)
Cause: The migration version number already exists in the `versioninfo` table from a previous run.
Solution: Check `SELECT * FROM public.versioninfo ORDER BY version;`. If the number exists, the migration was already applied. Use the next available number for new migrations.

### Down() fails with "table does not exist"
Cause: Typo in table name in `Down()`, or the table was already dropped by a different migration.
Solution: Verify table names match exactly between `Up()` and `Down()`. Always test the Up/Down/Up cycle locally before committing.

### NOT NULL column fails on existing table
Cause: Adding a NOT NULL column to a table with existing rows without providing a default value.
Solution: Use the three-step pattern: add as nullable, populate data, then alter to NOT NULL. See `references/alter-table-patterns.md` Pattern 25.

### Foreign key creation fails
Cause: The referenced table does not exist yet, or the column types do not match (e.g., FK is INT but PK is GUID).
Solution: Ensure the referenced table is created in an earlier migration. Verify both columns use the same type (both `AsGuid()` or both `AsInt32()`).

### CREATE INDEX CONCURRENTLY fails
Cause: The migration is running inside a transaction. PostgreSQL requires `CONCURRENTLY` to run outside a transaction.
Solution: Add `TransactionBehavior.None` to the migration attribute: `[Migration(N, TransactionBehavior.None)]`.

### Version conflict between branches
Cause: Two developers used the same migration number on different branches.
Solution: The second developer to merge must renumber their migration to the next available number. Communicate migration numbers with the team before starting.

---

## Related

- **Domain Layer:** `create-domain` -- Entities, validators, value objects, domain exceptions
- **Application Layer:** `create-use-case` -- Use cases, Command/Handler pattern, transaction management
- **Infrastructure Layer:** `create-repository` -- Repository implementations, NHibernate mappers, Unit of Work
- **WebApi Layer:** `create-endpoint` -- Endpoints, request/response models, error handling
