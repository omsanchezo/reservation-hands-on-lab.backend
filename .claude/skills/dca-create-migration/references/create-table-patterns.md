# Create Table Patterns

Patterns for creating new database tables with FluentMigrator.

---

## Pattern 1: Simple Table

Basic table with a GUID primary key and simple columns.

```csharp
using FluentMigrator;

namespace {ProjectName}.migrations;

[Migration(23)]
public class M023CreateRolesTable : Migration
{
    private readonly string _tableName = "roles";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
              .InSchema(_schemaName)
              .WithColumn("id").AsGuid().PrimaryKey()
              .WithColumn("name").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(_tableName)
            .InSchema(_schemaName);
    }
}
```

Key points:
- GUID primary key is the APSYS standard
- `AsString()` without length maps to TEXT (unlimited) in PostgreSQL
- `Down()` drops the entire table

---

## Pattern 2: Table with Multiple Columns and Constraints

Table with various string lengths, unique constraints, and datetime columns.

```csharp
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
        Delete.Table(_tableName)
            .InSchema(_schemaName);
    }
}
```

Key points:
- `AsString(50)` maps to VARCHAR(50) -- use specific lengths for constrained fields
- `.Unique()` creates an inline unique constraint
- Multiple constraints chain on a single column definition

---

## Pattern 3: Table with Default Values

Table using `SystemMethods` for automatic timestamps and nullable columns.

```csharp
[Migration(30)]
public class M030CreateAuditLogsTable : Migration
{
    private readonly string _tableName = "audit_logs";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
              .InSchema(_schemaName)
              .WithColumn("id").AsGuid().PrimaryKey()
              .WithColumn("user_id").AsGuid().NotNullable()
              .WithColumn("action").AsString(100).NotNullable()
              .WithColumn("entity_type").AsString(50).NotNullable()
              .WithColumn("entity_id").AsString(50).NotNullable()
              .WithColumn("timestamp").AsDateTime().NotNullable()
                  .WithDefault(SystemMethods.CurrentDateTime)
              .WithColumn("ip_address").AsString(45).Nullable()
              .WithColumn("user_agent").AsString(500).Nullable();
    }

    public override void Down()
    {
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

Key points:
- `WithDefault(SystemMethods.CurrentDateTime)` maps to `NOW()` in PostgreSQL, `GETDATE()` in SQL Server
- Mark columns as `.Nullable()` explicitly when NULL is allowed
- String length 45 for IP addresses covers IPv6 max length

---

## Pattern 4: Table with Decimal and Numeric Types

Table using various numeric types, booleans, and static default values.

```csharp
[Migration(31)]
public class M031CreateProductsTable : Migration
{
    private readonly string _tableName = "products";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
              .InSchema(_schemaName)
              .WithColumn("id").AsGuid().PrimaryKey()
              .WithColumn("name").AsString(200).NotNullable()
              .WithColumn("description").AsString(1000).Nullable()
              .WithColumn("price").AsDecimal(18, 2).NotNullable()
              .WithColumn("stock").AsInt32().NotNullable().WithDefaultValue(0)
              .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
              .WithColumn("weight_kg").AsDouble().Nullable()
              .WithColumn("created_at").AsDateTime().NotNullable()
                  .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

Key points:
- `AsDecimal(18, 2)` maps to NUMERIC(18,2) -- use for monetary values
- `WithDefaultValue(0)` for static defaults vs `WithDefault(SystemMethods.*)` for dynamic defaults
- `AsBoolean()` maps to BOOLEAN in PostgreSQL
- `AsDouble()` maps to DOUBLE PRECISION -- use for approximate floating-point values

---

## Create Table with Indexes

When creating a table, add indexes for columns that will be frequently queried:

```csharp
[Migration(60)]
public class M060CreateOrdersTable : Migration
{
    private readonly string _tableName = "orders";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Create.Table(_tableName)
            .InSchema(_schemaName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("order_number").AsString(50).NotNullable().Unique()
            .WithColumn("customer_id").AsGuid().NotNullable()
            .WithColumn("status").AsString(20).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("ix_orders_customer_id")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("customer_id");

        Create.Index("ix_orders_status")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("status");

        Create.Index("ix_orders_created_at")
            .OnTable(_tableName).InSchema(_schemaName)
            .OnColumn("created_at");
    }

    public override void Down()
    {
        // Indexes are automatically dropped when the table is dropped
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```

Key points:
- Add indexes in the same migration as the table creation
- `Down()` only needs `Delete.Table()` -- indexes are dropped automatically with the table
- Use `ix_` prefix for index names (APSYS convention)
- Index FK columns (`customer_id`) and columns used in WHERE/ORDER BY clauses

---

## Migration Base Template

Use this template as a starting point for any new create-table migration:

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
        Create.Table(_tableName)
              .InSchema(_schemaName)
              .WithColumn("id").AsGuid().PrimaryKey()
              // Add columns here
              ;
    }

    public override void Down()
    {
        Delete.Table(_tableName).InSchema(_schemaName);
    }
}
```
