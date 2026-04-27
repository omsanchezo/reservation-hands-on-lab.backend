# DAOs (Data Access Objects) Reference

DAOs are read-only projections for optimized queries.

## Key Differences from Entities

- No `AbstractDomainObject` inheritance (plain POCO)
- No validation
- No navigation properties (flatten relations)
- Have `SearchAll` field for full-text search
- Use `IReadOnlyRepository` (no write operations)

## Pattern

```csharp
namespace {project}.domain.daos;

public class UserDao
{
    public virtual Guid Id { get; set; }
    public virtual string Email { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual string RoleName { get; set; } = string.Empty;
    public virtual string SearchAll { get; set; } = string.Empty;
}
```

## SearchAll Pattern

- Concatenated field of all searchable properties
- Calculated via NHibernate Formula in mapping: `(Email + ' ' + Name + ' ' + RoleName)`
- Used for filtering: `x => x.SearchAll.Contains(searchTerm)`

## DAO vs Entity vs DTO

| | Entity | DAO | DTO |
|---|--------|-----|-----|
| Layer | Domain | Domain | WebApi |
| Identity | Yes (Id) | Has Id but no identity semantics | No |
| Validation | Yes | No | No |
| Methods | Yes | No | No |
| Read/Write | Read/Write | Read-only | Transfer |
| Base class | AbstractDomainObject | None (POCO) | None (POCO) |
| Navigation props | Yes | No (flattened) | No |

## Registration in IUnitOfWork

```csharp
public interface IUnitOfWork
{
    // Read-write
    IUserRepository Users { get; }

    // Read-only (DAOs)
    IReadOnlyRepository<UserDao, Guid> UserDaos { get; }
}
```

## Namespace

`{project}.domain.daos`

## Checklist

- [ ] POCO, no AbstractDomainObject
- [ ] Namespace: {project}.domain.daos
- [ ] Properties virtual with defaults
- [ ] SearchAll string field
- [ ] No navigation properties (flattened)
- [ ] Registered as IReadOnlyRepository in IUnitOfWork
