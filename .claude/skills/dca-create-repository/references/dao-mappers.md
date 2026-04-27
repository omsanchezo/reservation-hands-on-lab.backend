# DAO Mappers Reference

Complete reference for generating NHibernate `ClassMapping<T>` mappers for read-only Data Access Objects (DAOs).

---

## DAO Mapper Template

```csharp
using {project}.domain.daos;
using {project}.domain.resources;
using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace {project}.infrastructure.nhibernate.mappers;

public class {Entity}DaoMapper : ClassMapping<{Entity}Dao>
{
    public {Entity}DaoMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Mutable(false);                        // READ-ONLY (mandatory for DAOs)
        Table("{entity}_view");                // Maps to a database VIEW

        Id(x => x.Id, map =>
        {
            map.Column("id");
            map.Generator(Generators.Assigned);
            map.Type(NHibernateUtil.Guid);
        });

        // ... properties matching the view columns ...

        Property(x => x.SearchAll, map =>
        {
            map.Column("search_all");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });
    }
}
```

---

## Entity Mapper vs DAO Mapper

| Aspect | Entity Mapper | DAO Mapper |
|--------|--------------|------------|
| Source | Table | View |
| `Mutable()` | `true` (default) | **`false`** |
| Operations | CRUD | Read-only |
| Repository | `NHRepository<T, TKey>` | `NHReadOnlyRepository<T, TKey>` |
| Cascade/Relations | Yes (Bag, ManyToMany) | **No** |
| SearchAll | No | Yes |
| Joins/Denormalization | No (lazy loading) | Yes (flattened in view) |

---

## SearchAll Column Mapping

The `SearchAll` column is computed in the PostgreSQL view. It concatenates searchable fields, lowercased and unaccented, for full-text search:

```sql
CREATE OR REPLACE VIEW public.technical_standards_view AS
SELECT
    ts.id,
    ts.creation_date,
    ts.code,
    ts.name,
    ts.edition,
    ts.status,
    ts.type,
    CONCAT_WS(' ',
        LOWER(UNACCENT(ts.code)),
        LOWER(UNACCENT(ts.name)),
        LOWER(UNACCENT(ts.edition))
    ) AS search_all
FROM technical_standards ts;
```

**Pattern**: `CONCAT_WS(' ', LOWER(UNACCENT(field1)), LOWER(UNACCENT(field2)), ...)` ensures accent-insensitive, case-insensitive search.

---

## Complete Examples

### Example 1: TechnicalStandardDaoMapper

```csharp
public class TechnicalStandardDaoMapper : ClassMapping<TechnicalStandardDao>
{
    public TechnicalStandardDaoMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Mutable(false);
        Table("technical_standards_view");

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

        Property(x => x.SearchAll, map =>
        {
            map.Column("search_all");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });
    }
}
```

### Example 2: UserDaoMapper (with flattened relationships)

```csharp
public class UserDaoMapper : ClassMapping<UserDao>
{
    public UserDaoMapper()
    {
        Schema(AppSchemaResource.SchemaName);
        Mutable(false);
        Table("users_view");

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
        });

        Property(x => x.Name, map =>
        {
            map.Column("name");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });

        // Flattened: RoleNames instead of navigation property
        Property(x => x.RoleNames, map =>
        {
            map.Column("role_names");
            map.NotNullable(false);
            map.Type(NHibernateUtil.String);
        });

        Property(x => x.SearchAll, map =>
        {
            map.Column("search_all");
            map.NotNullable(true);
            map.Type(NHibernateUtil.String);
        });
    }
}
```

**Corresponding view:**

```sql
CREATE OR REPLACE VIEW public.users_view AS
SELECT
    u.id,
    u.email,
    u.name,
    STRING_AGG(r.name, ', ') AS role_names,
    CONCAT_WS(' ',
        LOWER(UNACCENT(u.email)),
        LOWER(UNACCENT(u.name))
    ) AS search_all
FROM users u
LEFT JOIN user_in_roles uir ON u.id = uir.user_id
LEFT JOIN roles r ON uir.role_id = r.id
GROUP BY u.id, u.email, u.name;
```
