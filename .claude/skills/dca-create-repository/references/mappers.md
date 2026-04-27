# Entity Mappers Reference

Complete reference for generating NHibernate `ClassMapping<T>` mappers for APSYS domain entities.

---

## ClassMapping Template

```csharp
using {project}.domain.entities;
using {project}.domain.resources;
using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace {project}.infrastructure.nhibernate.mappers;

/// <summary>
/// NHibernate mapping configuration for the <see cref="{Entity}"/> entity.
/// </summary>
public class {Entity}Mapper : ClassMapping<{Entity}>
{
    public {Entity}Mapper()
    {
        // 1. Schema
        Schema(AppSchemaResource.SchemaName);

        // 2. Table
        Table("{table_name}");

        // 3. Id
        Id(x => x.Id, map =>
        {
            map.Column("id");
            map.Generator(Generators.Assigned);
            map.Type(NHibernateUtil.Guid);
        });

        // 4. Properties
        Property(x => x.CreationDate, map =>
        {
            map.Column("creation_date");
            map.NotNullable(true);
            map.Type(NHibernateUtil.DateTime);
        });

        // ... more properties

        // 5. Relations (if applicable)
        // Bag, ManyToMany, ManyToOne, OneToMany
    }
}
```

---

## Configuration Order

```
1. Schema(AppSchemaResource.SchemaName)   <- DB schema
2. Table("table_name")                     <- Table name
3. Id(x => x.Id, ...)                     <- Primary key
4. Property(x => x.Prop, ...)             <- Properties
5. Bag(x => x.Collection, ...)            <- Relations
```

---

## NHibernateUtil Type Mapping

| .NET Type | NHibernateUtil | PostgreSQL | Mapper Example |
|-----------|----------------|------------|----------------|
| `Guid` | `NHibernateUtil.Guid` | `UUID` | `map.Type(NHibernateUtil.Guid)` |
| `string` | `NHibernateUtil.String` | `VARCHAR` | `map.Type(NHibernateUtil.String)` |
| `DateTime` | `NHibernateUtil.DateTime` | `TIMESTAMP` | `map.Type(NHibernateUtil.DateTime)` |
| `int` | `NHibernateUtil.Int32` | `INTEGER` | `map.Type(NHibernateUtil.Int32)` |
| `long` | `NHibernateUtil.Int64` | `BIGINT` | `map.Type(NHibernateUtil.Int64)` |
| `decimal` | `NHibernateUtil.Decimal` | `NUMERIC` | `map.Type(NHibernateUtil.Decimal)` |
| `bool` | `NHibernateUtil.Boolean` | `BOOLEAN` | `map.Type(NHibernateUtil.Boolean)` |
| `byte[]` | `NHibernateUtil.BinaryBlob` | `BYTEA` | `map.Type(NHibernateUtil.BinaryBlob)` |

---

## Restrictions

| Restriction | Method | Example |
|-------------|--------|---------|
| NOT NULL | `map.NotNullable(true)` | Required field |
| NULL allowed | `map.NotNullable(false)` | Optional field |
| UNIQUE | `map.Unique(true)` | Unique value in table |
| LENGTH | `map.Length(n)` | Max string length (VARCHAR(n)) |
| PRECISION | `map.Precision(n)` | Total digits for decimal |
| SCALE | `map.Scale(n)` | Decimal places |

---

## Many-to-Many (Bag + ManyToMany)

```csharp
// In UserMapper -- User is the OWNER of the relationship
Bag(x => x.Roles, map =>
{
    map.Schema(AppSchemaResource.SchemaName);
    map.Table("user_in_roles");           // Join table
    map.Key(k => k.Column("user_id"));    // FK to users
    map.Cascade(Cascade.All);             // Cascade operations
    map.Inverse(false);                   // User is owner
},
map => map.ManyToMany(m =>
{
    m.Column("role_id");                  // FK to roles
    m.Class(typeof(Role));                // Related entity
}));
```

**Rules:**
- Exactly ONE side must have `Inverse(false)` (the owner)
- The other side (if mapped) must have `Inverse(true)`
- Always set `Cascade(Cascade.All)` on the owner side
- `map.Schema()` must be set on the Bag for the join table

**Join table structure:**
```
users ──< user_in_roles >── roles
 (id)      (user_id, role_id)     (id)
```

---

## Many-to-One

```csharp
ManyToOne(x => x.Customer, map =>
{
    map.Column("customer_id");
    map.NotNullable(true);
    map.Cascade(Cascade.None);
});
```

## One-to-Many

```csharp
Bag(x => x.Orders, map =>
{
    map.Key(k => k.Column("customer_id"));
    map.Cascade(Cascade.All);
},
map => map.OneToMany());
```

---

## Complete Examples

### Example 1: Simple Entity (Role)

```csharp
using hashira.stone.backend.domain.entities;
using hashira.stone.backend.domain.resources;
using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace hashira.stone.backend.infrastructure.nhibernate.mappers;

/// <summary>
/// NHibernate mapping configuration for the <see cref="Role"/> entity.
/// </summary>
public class RoleMapper : ClassMapping<Role>
{
    public RoleMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Table("roles");

        Id(x => x.Id, map =>
        {
            map.Column("id");
            map.Generator(Generators.Assigned);
            map.Type(NHibernateUtil.Guid);
        });

        Property(x => x.Name, map =>
        {
            map.Column("name");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });
    }
}
```

### Example 2: Multiple Properties (TechnicalStandard)

```csharp
public class TechnicalStandardMapper : ClassMapping<TechnicalStandard>
{
    public TechnicalStandardMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Table("technical_standards");

        Id(x => x.Id, map =>
        {
            map.Column("id");
            map.Generator(Generators.Assigned);
            map.Type(NHibernateUtil.Guid);
        });

        Property(x => x.CreationDate, map =>
        {
            map.Column("creation_date");
            map.NotNullable(true);
            map.Type(NHibernateUtil.DateTime);
        });

        Property(x => x.Code, map =>
        {
            map.Column("code");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
            map.Unique(true);
        });

        Property(x => x.Name, map =>
        {
            map.Column("name");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });

        Property(x => x.Edition, map =>
        {
            map.Column("edition");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });

        Property(x => x.Status, map =>
        {
            map.Column("status");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });

        Property(x => x.Type, map =>
        {
            map.Column("type");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });
    }
}
```

### Example 3: Many-to-Many (User with Roles)

```csharp
/// <summary>
/// NHibernate mapping configuration for the <see cref="User"/> entity.
/// </summary>
public class UserMapper : ClassMapping<User>
{
    public UserMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Table("users");

        Id(x => x.Id, map =>
        {
            map.Column("id");
            map.Generator(Generators.Assigned);
            map.Type(NHibernateUtil.Guid);
        });

        Property(x => x.Email, map =>
        {
            map.Column("email");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
            map.Unique(true);
        });

        Property(x => x.Name, map =>
        {
            map.Column("name");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });

        Bag(x => x.Roles, map =>
        {
            map.Schema(AppSchemaResource.SchemaName);
            map.Table("user_in_roles");
            map.Key(k => k.Column("user_id"));
            map.Cascade(Cascade.All);
            map.Inverse(false);
        },
        map => map.ManyToMany(m =>
        {
            m.Column("role_id");
            m.Class(typeof(Role));
        }));
    }
}
```
