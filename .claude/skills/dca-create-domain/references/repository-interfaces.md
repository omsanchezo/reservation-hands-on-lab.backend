# Repository Interfaces Reference

Dependency Inversion: interfaces in Domain, implementations in Infrastructure.

## Interface Hierarchy

```
IReadOnlyRepository<T, TKey>    (Get, Count, GetManyAndCount)
    └── IRepository<T, TKey>    (+ Add, Save, Delete)
```

## IReadOnlyRepository Methods

| Method | Description |
|--------|-------------|
| `T Get(Expression<Func<T, bool>> where)` | Get one by condition (sync) |
| `Task<T> GetAsync(TKey id, CancellationToken ct)` | Get by Id |
| `long Count(Expression<Func<T, bool>> where)` | Count sync |
| `Task<long> CountAsync(Expression<Func<T, bool>> where, CancellationToken ct)` | Count async |
| `GetManyAndCountResult<T> GetManyAndCount(Expression<Func<T, bool>> where, int pageNumber, int pageSize, ...)` | Paginated query sync |
| `Task<GetManyAndCountResult<T>> GetManyAndCountAsync(...)` | Paginated query async |

## IRepository Adds

| Method | Description |
|--------|-------------|
| `void Add(T entity)` / `Task AddAsync(T entity)` | Insert NEW entity |
| `void Save(T entity)` / `Task SaveAsync(T entity)` | Update EXISTING entity |
| `void Delete(T entity)` / `Task DeleteAsync(T entity)` | Remove entity |

**Add vs Save:** Add for new entities, Save for existing. Wrong usage causes errors.

## Specific Repository Interfaces

```csharp
public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmail(string email);
}

public interface IRoleRepository : IRepository<Role, Guid> { }
```

## IUnitOfWork

```csharp
public interface IUnitOfWork
{
    // Repositories (read-write)
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }

    // Read-only (DAOs)
    IReadOnlyRepository<UserDao, Guid> UserDaos { get; }

    // Transaction management
    void Commit();
    void Rollback();
    void BeginTransaction();
    void ResetTransaction();
    bool IsActiveTransaction { get; }
}
```

## Support Types

```csharp
public class GetManyAndCountResult<T>
{
    public IList<T> Items { get; set; }
    public long Count { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public SortingCriteria? Sorting { get; set; }
}

public class SortingCriteria
{
    public string PropertyName { get; set; }
    public SortingCriteriaType Type { get; set; }
}

public enum SortingCriteriaType { Ascending, Descending }
```

## Namespace

`{project}.domain.interfaces`

## Checklist

- [ ] Inherits IRepository or IReadOnlyRepository
- [ ] Namespace: {project}.domain.interfaces
- [ ] Custom domain methods defined
- [ ] Registered in IUnitOfWork
- [ ] Implementations go in Infrastructure layer
