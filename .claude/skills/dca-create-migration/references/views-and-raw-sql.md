# Views and Raw SQL

Patterns for creating database views, executing custom SQL, stored functions, and schema management.

---

## Pattern 17: Simple View

```csharp
[Migration(26)]
public class M026TechnicalStandardsView : Migration
{
    private readonly string _viewName = "technical_standards_view";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        var fullViewName = $"{_schemaName}.{_viewName}";
        var sql = $@"
            CREATE OR REPLACE VIEW {fullViewName} AS
            SELECT
                id,
                code,
                creation_date,
                name,
                edition,
                status,
                type,
                lower(unaccent(code || ' ' || name || ' ' || edition)) as search_all
            FROM public.technical_standards;
        ";
        Execute.Sql(sql);
    }

    public override void Down()
    {
        var fullViewName = $"{_schemaName}.{_viewName}";
        Execute.Sql($@"DROP VIEW IF EXISTS {fullViewName};");
    }
}
```

Key points:
- Use `CREATE OR REPLACE VIEW` for idempotent creation
- Computed columns (e.g., `search_all`) can combine and transform data for search
- `Down()` uses `DROP VIEW IF EXISTS` for safe rollback
- Views are read-only by default in PostgreSQL

---

## Pattern 18: View with JOIN

```csharp
[Migration(44)]
public class M044CreateOrdersWithUserView : Migration
{
    private readonly string _viewName = "orders_with_user_view";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        var fullViewName = $"{_schemaName}.{_viewName}";
        var sql = $@"
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
        ";
        Execute.Sql(sql);
    }

    public override void Down()
    {
        var fullViewName = $"{_schemaName}.{_viewName}";
        Execute.Sql($"DROP VIEW IF EXISTS {fullViewName};");
    }
}
```

Key points:
- Use column aliases (`AS order_id`) to disambiguate columns from different tables
- Reference schema-qualified table names (`{_schemaName}.orders`)
- Useful for frequently executed read-only queries that span multiple tables

---

## Pattern 19: Execute Custom SQL (Check Constraints)

```csharp
[Migration(45)]
public class M045AddCustomConstraint : Migration
{
    private readonly string _tableName = "products";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Execute.Sql($@"
            ALTER TABLE {_schemaName}.{_tableName}
            ADD CONSTRAINT chk_price_positive
            CHECK (price > 0);
        ");
    }

    public override void Down()
    {
        Execute.Sql($@"
            ALTER TABLE {_schemaName}.{_tableName}
            DROP CONSTRAINT IF EXISTS chk_price_positive;
        ");
    }
}
```

Key points:
- Use `Execute.Sql()` for any SQL that FluentMigrator API does not support natively
- CHECK constraints enforce data integrity rules at the database level
- `Down()` uses `IF EXISTS` for safe rollback

---

## Pattern 20: Create Function / Stored Procedure

```csharp
[Migration(46)]
public class M046CreateGetUserByEmailFunction : Migration
{
    private readonly string _functionName = "get_user_by_email";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Execute.Sql($@"
            CREATE OR REPLACE FUNCTION {_schemaName}.{_functionName}(p_email VARCHAR)
            RETURNS TABLE(
                id UUID,
                name VARCHAR,
                email VARCHAR,
                created_at TIMESTAMP
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT u.id, u.name, u.email, u.created_at
                FROM {_schemaName}.users u
                WHERE u.email = p_email;
            END;
            $$ LANGUAGE plpgsql;
        ");
    }

    public override void Down()
    {
        Execute.Sql($@"
            DROP FUNCTION IF EXISTS {_schemaName}.{_functionName}(VARCHAR);
        ");
    }
}
```

Key points:
- Use `CREATE OR REPLACE FUNCTION` for idempotent creation
- `RETURNS TABLE(...)` defines the result set structure
- `DROP FUNCTION` must include the parameter signature to identify the correct overload
- PL/pgSQL is the standard procedural language for PostgreSQL

---

## Pattern 23: Rename Table

```csharp
[Migration(49)]
public class M049RenameProductsTable : Migration
{
    private readonly string _oldTableName = "products";
    private readonly string _newTableName = "items";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Rename.Table(_oldTableName)
              .InSchema(_schemaName)
              .To(_newTableName);
    }

    public override void Down()
    {
        Rename.Table(_newTableName)
              .InSchema(_schemaName)
              .To(_oldTableName);
    }
}
```

Key points:
- Preserves all data, constraints, and indexes
- Update views, foreign keys, and application code referencing the old name

---

## Pattern 24: Create Schema

```csharp
[Migration(50)]
public class M050CreateReportsSchema : Migration
{
    private readonly string _schemaName = "reports";

    public override void Up()
    {
        if (!Schema.Schema(_schemaName).Exists())
        {
            Create.Schema(_schemaName);
        }
    }

    public override void Down()
    {
        Delete.Schema(_schemaName);
    }
}
```

Key points:
- `Schema.Schema().Exists()` check ensures idempotent execution
- Useful for organizing tables by module or bounded context
- Tables in custom schemas use `CustomVersionTableMetaData.SchemaNameValue` or the custom schema name

---

## When to Use Raw SQL vs FluentMigrator API

| Scenario | Use |
|----------|-----|
| Create/alter/delete table | FluentMigrator API |
| Create/delete index | FluentMigrator API |
| Create/delete foreign key | FluentMigrator API |
| Filtered index (WHERE clause) | `Execute.Sql()` |
| CHECK constraint | `Execute.Sql()` |
| View (CREATE VIEW) | `Execute.Sql()` |
| Function / Stored procedure | `Execute.Sql()` |
| Database extension (unaccent, uuid) | `Execute.Sql()` |
| Batch data updates | `Execute.Sql()` |
| `CREATE INDEX CONCURRENTLY` | `Execute.Sql()` with `TransactionBehavior.None` |
