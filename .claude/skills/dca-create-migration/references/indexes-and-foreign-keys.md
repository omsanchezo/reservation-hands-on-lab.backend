# Indexes and Foreign Keys

Patterns for creating indexes, unique constraints, and foreign key relationships.

---

## Pattern 10: Simple Index

```csharp
[Migration(37)]
public class M037AddIndexToUsersEmail : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _indexName = "ix_users_email";

    public override void Up()
    {
        Create.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName)
              .OnColumn("email")
              .Ascending();
    }

    public override void Down()
    {
        Delete.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName);
    }
}
```

Key points:
- Always use explicit index names with the `ix_{table}_{column}` convention
- Specify sort direction with `.Ascending()` or `.Descending()`
- Index columns used in WHERE clauses, JOIN conditions, and ORDER BY

---

## Pattern 11: Unique Index

```csharp
[Migration(38)]
public class M038AddUniqueIndexToUsersEmail : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _indexName = "ix_users_email";

    public override void Up()
    {
        Create.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName)
              .OnColumn("email")
              .Ascending()
              .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName);
    }
}
```

Key points:
- `.WithOptions().Unique()` enforces uniqueness at the database level
- More efficient than an inline `.Unique()` constraint for lookup-heavy columns
- Prevents duplicate values in the indexed column(s)

---

## Pattern 12: Composite Index (Multiple Columns)

```csharp
[Migration(39)]
public class M039AddCompositeIndexToOrders : Migration
{
    private readonly string _tableName = "orders";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _indexName = "ix_orders_user_status";

    public override void Up()
    {
        Create.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName)
              .OnColumn("user_id").Ascending()
              .OnColumn("status").Ascending()
              .OnColumn("created_at").Descending();
    }

    public override void Down()
    {
        Delete.Index(_indexName)
              .OnTable(_tableName)
              .InSchema(_schemaName);
    }
}
```

Key points:
- Chain multiple `.OnColumn()` calls for composite indexes
- Column order matters -- put the most selective column first
- Mix `.Ascending()` and `.Descending()` to match query patterns
- Name includes the key columns: `ix_{table}_{col1}_{col2}`

---

## Pattern 13: Filtered Index (PostgreSQL)

For partial indexes that only include rows matching a condition, use raw SQL:

```csharp
[Migration(40)]
public class M040AddFilteredIndexToUsers : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _indexName = "ix_users_active_email";

    public override void Up()
    {
        Execute.Sql($@"
            CREATE INDEX {_indexName}
            ON {_schemaName}.{_tableName} (email)
            WHERE is_active = true;
        ");
    }

    public override void Down()
    {
        Execute.Sql($"DROP INDEX IF EXISTS {_schemaName}.{_indexName};");
    }
}
```

Key points:
- Filtered indexes reduce index size by only indexing rows that match the WHERE condition
- Must use `Execute.Sql()` since FluentMigrator API does not support WHERE clauses on indexes
- Ideal for columns where most queries filter on a specific value (e.g., active records only)

---

## Concurrent Index Creation (Large Tables)

For tables with millions of rows in production, use `CREATE INDEX CONCURRENTLY` to avoid blocking writes:

```csharp
[Migration(61, TransactionBehavior.None)] // CONCURRENTLY requires no transaction
public class M061CreateIndexOnLargeTable : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_orders_customer_created
            ON public.orders(customer_id, created_at);
        ");
    }

    public override void Down()
    {
        Execute.Sql(@"
            DROP INDEX IF EXISTS public.ix_orders_customer_created;
        ");
    }
}
```

Key points:
- **Requires** `TransactionBehavior.None` -- PostgreSQL cannot run `CONCURRENTLY` inside a transaction
- Slower than a regular index build but does not block writes
- Use when: table has > 1M rows, production has active traffic, downtime is not acceptable
- May leave an INVALID index if it fails -- check with `\di` and recreate if needed

---

## Pattern 14: Simple Foreign Key

```csharp
[Migration(41)]
public class M041AddForeignKeyToOrders : Migration
{
    private readonly string _ordersTable = "orders";
    private readonly string _usersTable = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _fkName = "fk_orders_user_id";

    public override void Up()
    {
        Create.ForeignKey(_fkName)
              .FromTable(_ordersTable)
              .InSchema(_schemaName)
              .ForeignColumn("user_id")
              .ToTable(_usersTable)
              .InSchema(_schemaName)
              .PrimaryColumn("id");
    }

    public override void Down()
    {
        Delete.ForeignKey(_fkName)
              .OnTable(_ordersTable)
              .InSchema(_schemaName);
    }
}
```

Key points:
- Always use explicit FK names with `fk_{from_table}_{column}` convention
- `FromTable()` = child table (the one with the FK column)
- `ToTable()` = parent table (the one with the PK)
- The referenced table must already exist

---

## Pattern 15: Foreign Key with Cascade Delete

```csharp
[Migration(42)]
public class M042AddForeignKeyWithCascade : Migration
{
    private readonly string _orderItemsTable = "order_items";
    private readonly string _ordersTable = "orders";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;
    private readonly string _fkName = "fk_order_items_order_id";

    public override void Up()
    {
        Create.ForeignKey(_fkName)
              .FromTable(_orderItemsTable)
              .InSchema(_schemaName)
              .ForeignColumn("order_id")
              .ToTable(_ordersTable)
              .InSchema(_schemaName)
              .PrimaryColumn("id")
              .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.ForeignKey(_fkName)
              .OnTable(_orderItemsTable)
              .InSchema(_schemaName);
    }
}
```

Delete rule options:
- `Rule.Cascade` -- delete child rows when parent is deleted
- `Rule.SetNull` -- set FK column to NULL when parent is deleted
- `Rule.SetDefault` -- set FK column to default value when parent is deleted
- `Rule.NoAction` -- prevent deletion if child rows exist (default)

---

## Pattern 16: Junction Table with Multiple Foreign Keys

Many-to-many relationship implemented as a junction table with composite primary key and cascade delete:

```csharp
[Migration(43)]
public class M043CreateJunctionTableWithFKs : Migration
{
    private readonly string _junctionTable = "user_in_roles";
    private readonly string _usersTable = "users";
    private readonly string _rolesTable = "roles";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        // 1. Create junction table
        Create.Table(_junctionTable)
              .InSchema(_schemaName)
              .WithColumn("user_id").AsGuid().NotNullable()
              .WithColumn("role_id").AsGuid().NotNullable();

        // 2. Composite primary key
        Create.PrimaryKey($"pk_{_junctionTable}")
              .OnTable(_junctionTable)
              .WithSchema(_schemaName)
              .Columns("user_id", "role_id");

        // 3. Foreign key to users
        Create.ForeignKey($"fk_{_junctionTable}_user_id")
              .FromTable(_junctionTable)
              .InSchema(_schemaName)
              .ForeignColumn("user_id")
              .ToTable(_usersTable)
              .InSchema(_schemaName)
              .PrimaryColumn("id")
              .OnDelete(System.Data.Rule.Cascade);

        // 4. Foreign key to roles
        Create.ForeignKey($"fk_{_junctionTable}_role_id")
              .FromTable(_junctionTable)
              .InSchema(_schemaName)
              .ForeignColumn("role_id")
              .ToTable(_rolesTable)
              .InSchema(_schemaName)
              .PrimaryColumn("id")
              .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table(_junctionTable).InSchema(_schemaName);
    }
}
```

Key points:
- Junction table uses composite PK instead of a surrogate GUID
- Both FKs use cascade delete -- deleting a user or role removes the association
- `Down()` only needs `Delete.Table()` -- FKs and PK are dropped automatically with the table
- Name convention for junction tables: `{entity1}_in_{entity2}` or `{entity1}_{entity2}`
