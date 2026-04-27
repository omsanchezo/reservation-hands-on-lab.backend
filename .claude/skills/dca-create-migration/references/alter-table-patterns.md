# Alter Table Patterns

Patterns for modifying existing database tables with FluentMigrator.

---

## Pattern 5: Add Column to Existing Table

```csharp
[Migration(32)]
public class M032AddEmailToUsers : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AddColumn("email").AsString(255).Nullable();
    }

    public override void Down()
    {
        Delete.Column("email")
              .FromTable(_tableName)
              .InSchema(_schemaName);
    }
}
```

Key points:
- New columns on existing tables should typically be `.Nullable()` to avoid breaking existing rows
- If NOT NULL is required, use the three-step data migration pattern (Pattern 25)
- `Down()` deletes only the added column

---

## Pattern 6: Add Multiple Columns

```csharp
[Migration(33)]
public class M033AddAuditColumnsToUsers : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AddColumn("created_at").AsDateTime().NotNullable()
                 .WithDefault(SystemMethods.CurrentDateTime)
             .AddColumn("updated_at").AsDateTime().Nullable()
             .AddColumn("created_by").AsGuid().Nullable()
             .AddColumn("updated_by").AsGuid().Nullable();
    }

    public override void Down()
    {
        Delete.Column("created_at").FromTable(_tableName).InSchema(_schemaName);
        Delete.Column("updated_at").FromTable(_tableName).InSchema(_schemaName);
        Delete.Column("created_by").FromTable(_tableName).InSchema(_schemaName);
        Delete.Column("updated_by").FromTable(_tableName).InSchema(_schemaName);
    }
}
```

Key points:
- Chain multiple `AddColumn()` calls on a single `Alter.Table()`
- Audit trail pattern: `created_at`, `updated_at`, `created_by`, `updated_by`
- `created_at` can be NOT NULL with a default; the others are Nullable for existing rows
- `Down()` must delete each column individually

---

## Pattern 7: Alter Column Type

```csharp
[Migration(34)]
public class M034AlterUserNameLength : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AlterColumn("name").AsString(500).NotNullable();
    }

    public override void Down()
    {
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AlterColumn("name").AsString(255).NotNullable();
    }
}
```

Key points:
- `AlterColumn()` changes type, length, or nullability
- `Down()` must revert to the original type/length
- Widening a column (255 -> 500) is safe; narrowing may truncate data

---

## Pattern 8: Rename Column

```csharp
[Migration(35)]
public class M035RenameUserEmailColumn : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Rename.Column("email")
              .OnTable(_tableName)
              .InSchema(_schemaName)
              .To("email_address");
    }

    public override void Down()
    {
        Rename.Column("email_address")
              .OnTable(_tableName)
              .InSchema(_schemaName)
              .To("email");
    }
}
```

Key points:
- `Rename.Column()` preserves data and constraints
- Update NHibernate mappings and application code to use the new column name
- `Down()` reverses the rename

---

## Pattern 9: Delete Column

```csharp
[Migration(36)]
public class M036RemoveObsoleteColumns : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        Delete.Column("legacy_field")
              .FromTable(_tableName)
              .InSchema(_schemaName);
    }

    public override void Down()
    {
        // WARNING: Re-adding the column does not recover data
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AddColumn("legacy_field").AsString().Nullable();
    }
}
```

Key points:
- Deleting a column is **irreversible** for data -- `Down()` recreates the column but data is lost
- Always back up the database before deleting columns in production
- Ensure no application code references the column before removing it

---

## Pattern 25: Add Column with Data Migration (Three-Step Pattern)

Use this pattern when adding a NOT NULL column to a table that already has rows.

```csharp
[Migration(51)]
public class M051AddFullNameToUsers : Migration
{
    private readonly string _tableName = "users";
    private readonly string _schemaName = CustomVersionTableMetaData.SchemaNameValue;

    public override void Up()
    {
        // Step 1: Add column as NULLABLE
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AddColumn("full_name").AsString(500).Nullable();

        // Step 2: Populate from existing data
        Execute.Sql($@"
            UPDATE {_schemaName}.{_tableName}
            SET full_name = CONCAT(first_name, ' ', last_name)
            WHERE full_name IS NULL;
        ");

        // Step 3: Make NOT NULL after population
        Alter.Table(_tableName)
             .InSchema(_schemaName)
             .AlterColumn("full_name").AsString(500).NotNullable();
    }

    public override void Down()
    {
        Delete.Column("full_name")
              .FromTable(_tableName)
              .InSchema(_schemaName);
    }
}
```

Key points:
- Nullable first to allow the UPDATE to run on existing rows
- NOT NULL enforced only after all rows have been populated
- Essential for zero-downtime deployments with existing data

---

## Zero-Downtime Column Addition

For high-availability systems, split the three-step pattern across separate migrations:

**Migration 1: Add nullable column**
```csharp
[Migration(70)]
public class M070AddFullNameStep1 : Migration
{
    public override void Up()
    {
        Alter.Table("users").InSchema("public")
            .AddColumn("full_name").AsString(500).Nullable();
    }

    public override void Down()
    {
        Delete.Column("full_name").FromTable("users").InSchema("public");
    }
}
```

**Migration 2: Populate existing data**
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

**Migration 3: Make NOT NULL**
```csharp
[Migration(72)]
public class M072AddFullNameStep3 : Migration
{
    public override void Up()
    {
        Alter.Table("users").InSchema("public")
            .AlterColumn("full_name").AsString(500).NotNullable();
    }

    public override void Down()
    {
        Alter.Table("users").InSchema("public")
            .AlterColumn("full_name").AsString(500).Nullable();
    }
}
```

This approach allows deploying application code between steps to start writing to the new column before making it required.

---

## Rename Table

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
- `Rename.Table()` preserves all data, constraints, and indexes
- Update all NHibernate mappings, views, and foreign keys that reference the old table name
