# Repositories Reference

Complete reference for NHibernate repository implementations in APSYS backend projects.

---

## Repository Hierarchy

```
IReadOnlyRepository<T, TKey>  (Domain interface)
        |
NHReadOnlyRepository<T, TKey> (Infrastructure - read operations)
        |
    IRepository<T, TKey>      (Domain interface)
        |
    NHRepository<T, TKey>     (Infrastructure - CRUD + validation)
        |
    NHOrderRepository          (Specific - business methods)
```

---

## NHReadOnlyRepository (Full Code)

```csharp
using System.Linq.Expressions;
using {project}.domain.interfaces.repositories;
using {project}.infrastructure.nhibernate.filtering;
using System.Linq.Dynamic.Core;
using NHibernate;
using NHibernate.Linq;

namespace {project}.infrastructure.nhibernate;

public class NHReadOnlyRepository<T, TKey>(ISession session) : IReadOnlyRepository<T, TKey>
    where T : class, new()
{
    protected internal readonly ISession _session = session;

    // COUNT
    public int Count()
        => _session.QueryOver<T>().RowCount();

    public int Count(Expression<Func<T, bool>> query)
        => _session.Query<T>().Where(query).Count();

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _session.Query<T>().CountAsync(cancellationToken);

    public Task<int> CountAsync(
        Expression<Func<T, bool>> query,
        CancellationToken cancellationToken = default)
        => _session.Query<T>().Where(query).CountAsync(cancellationToken);

    // GET BY ID
    public T Get(TKey id)
        => _session.Get<T>(id);

    public Task<T> GetAsync(TKey id, CancellationToken cancellationToken = default)
        => _session.GetAsync<T>(id, cancellationToken);

    // GET ALL / WITH FILTER
    public IEnumerable<T> Get()
        => _session.Query<T>();

    public IEnumerable<T> Get(Expression<Func<T, bool>> query)
        => _session.Query<T>().Where(query);

    public async Task<IEnumerable<T>> GetAsync(CancellationToken cancellationToken = default)
        => await _session.Query<T>().ToListAsync(cancellationToken);

    public async Task<IEnumerable<T>> GetAsync(
        Expression<Func<T, bool>> query,
        CancellationToken cancellationToken = default)
        => await _session.Query<T>()
            .Where(query)
            .ToListAsync(cancellationToken);

    // GET WITH PAGINATION
    public IEnumerable<T> Get(
        Expression<Func<T, bool>> query,
        int page,
        int pageSize,
        SortingCriteria sortingCriteria)
        => _session.Query<T>()
            .Where(query)
            .OrderBy(sortingCriteria.ToExpression())
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

    // GET MANY AND COUNT
    public GetManyAndCountResult<T> GetManyAndCount(string? query, string defaultSorting)
    {
        var (expression, pageNumber, pageSize, sortingCriteria) = PrepareQuery(query, defaultSorting);
        var items = this.Get(expression, pageNumber, pageSize, sortingCriteria);
        var total = this.Count(expression);
        return new GetManyAndCountResult<T>(items, total, pageNumber, pageSize, sortingCriteria);
    }

    public async Task<GetManyAndCountResult<T>> GetManyAndCountAsync(
        string? query,
        string defaultSorting,
        CancellationToken cancellationToken = default)
    {
        var (expression, pageNumber, pageSize, sortingCriteria) = PrepareQuery(query, defaultSorting);

        // Execute queries sequentially to avoid DataReader conversion issues
        var total = await _session.Query<T>()
            .Where(expression)
            .CountAsync(cancellationToken);

        var items = await _session.Query<T>()
            .OrderBy(sortingCriteria.ToExpression())
            .Where(expression)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GetManyAndCountResult<T>(items, total, pageNumber, pageSize, sortingCriteria);
    }

    // PRIVATE HELPERS
    private static (
        Expression<Func<T, bool>> expression,
        int pageNumber,
        int pageSize,
        SortingCriteria sortingCriteria)
        PrepareQuery(string? query, string defaultSorting)
    {
        var queryString = string.IsNullOrEmpty(query) ? string.Empty : query;
        QueryStringParser queryStringParser = new(queryString);

        int pageNumber = queryStringParser.ParsePageNumber();
        int pageSize = queryStringParser.ParsePageSize();

        Sorting sorting = queryStringParser.ParseSorting<T>(defaultSorting);
        SortingCriteriaType directions = sorting.Direction == QueryStringParser.GetDescendingValue()
            ? SortingCriteriaType.Descending
            : SortingCriteriaType.Ascending;
        SortingCriteria sortingCriteria = new(sorting.By, directions);

        IList<FilterOperator> filters = queryStringParser.ParseFilterOperators<T>();
        QuickSearch? quickSearch = queryStringParser.ParseQuery<T>();
        var expression = FilterExpressionParser.ParsePredicate<T>(filters);
        if (quickSearch != null)
            expression = FilterExpressionParser.ParseQueryValuesToExpression(expression, quickSearch);

        return (expression, pageNumber, pageSize, sortingCriteria);
    }
}
```

---

## NHRepository (Full Code)

```csharp
using {project}.domain.exceptions;
using {project}.domain.interfaces.repositories;
using FluentValidation;
using NHibernate;

namespace {project}.infrastructure.nhibernate;

public abstract class NHRepository<T, TKey> : NHReadOnlyRepository<T, TKey>, IRepository<T, TKey>
    where T : class, new()
{
    private readonly AbstractValidator<T> validator;

    protected NHRepository(ISession session, IServiceProvider serviceProvider)
        : base(session)
    {
        Type genericType = typeof(AbstractValidator<>).MakeGenericType(typeof(T));
        this.validator = serviceProvider.GetService(genericType) as AbstractValidator<T>
            ?? throw new InvalidOperationException($"Validator for {typeof(T)} could not be created");
    }

    // ADD (Sync -- with automatic validation)
    public T Add(T item)
    {
        var validationResult = this.validator.Validate(item);
        if (!validationResult.IsValid)
            throw new InvalidDomainException(validationResult.Errors);

        this._session.Save(item);
        this.FlushWhenNotActiveTransaction();
        return item;
    }

    // ADD (Async -- without automatic validation, use in CreateAsync)
    public Task AddAsync(T item)
        => this._session.SaveAsync(item);

    // SAVE/UPDATE (Sync -- with automatic validation)
    public T Save(T item)
    {
        var validationResult = this.validator.Validate(item);
        if (!validationResult.IsValid)
            throw new InvalidDomainException(validationResult.Errors);

        this._session.Update(item);
        this.FlushWhenNotActiveTransaction();
        return item;
    }

    // SAVE/UPDATE (Async -- without automatic validation)
    public Task SaveAsync(T item)
        => this._session.UpdateAsync(item);

    // DELETE
    public void Delete(T item)
    {
        this._session.Delete(item);
        this.FlushWhenNotActiveTransaction();
    }

    public Task DeleteAsync(T item)
        => this._session.DeleteAsync(item);

    // HELPERS
    protected internal bool IsTransactionActive()
        => this._session.GetCurrentTransaction() != null
           && this._session.GetCurrentTransaction().IsActive;

    protected internal void FlushWhenNotActiveTransaction()
    {
        var currentTransaction = this._session.GetCurrentTransaction();
        if (currentTransaction == null || !currentTransaction.IsActive)
            this._session.Flush();
    }
}
```

---

## Specific Repository Patterns

### CreateAsync Pattern

```csharp
public async Task<Entity> CreateAsync(string param1, string param2)
{
    // 1. Create entity
    var entity = new Entity(param1, param2);

    // 2. Domain validation (Level 1)
    if (!entity.IsValid())
        throw new InvalidDomainException(entity.Validate());

    // 3. Business validation - duplicates (Level 2)
    var count = await this.CountAsync(
        e => e.UniqueField.ToLowerInvariant() == param1.ToLowerInvariant());
    if (count > 0)
        throw new DuplicatedDomainException($"Entity with '{param1}' already exists.");

    // 4. Persist + flush
    await AddAsync(entity);
    this.FlushWhenNotActiveTransaction();
    return entity;
}
```

### UpdateAsync Pattern

```csharp
public async Task<Entity> UpdateAsync(Guid id, string code, string name)
{
    // 1. Get existing entity
    var entity = await _session.Query<Entity>()
        .Where(e => e.Id == id)
        .SingleOrDefaultAsync();

    if (entity == null)
        throw new ResourceNotFoundException($"Entity with id '{id}' does not exist.");

    // 2. Check duplicates (exclude self)
    var existingWithCode = await GetByCodeAsync(code);
    if (existingWithCode != null && existingWithCode.Id != id)
        throw new DuplicatedDomainException($"Entity with code '{code}' already exists.");

    // 3. Update properties
    entity.Code = code;
    entity.Name = name;

    // 4. Domain validation
    if (!entity.IsValid())
        throw new InvalidDomainException(entity.Validate());

    // 5. Persist + flush
    await _session.UpdateAsync(entity);
    this.FlushWhenNotActiveTransaction();
    return entity;
}
```

### GetByXXXAsync Pattern

```csharp
public async Task<Entity?> GetByCodeAsync(string code)
{
    if (string.IsNullOrWhiteSpace(code))
        return null;

    return await _session.Query<Entity>()
        .Where(e => e.Code.ToLowerInvariant() == code.ToLowerInvariant())
        .SingleOrDefaultAsync();
}
```

### HQL with Unaccent (accent-insensitive search)

```csharp
public async Task<Entity?> GetByCodeAsync(string code)
{
    if (string.IsNullOrWhiteSpace(code))
        return null;

    var hql = @"
        from TechnicalStandard ts
        where lower(unaccent(ts.Code)) = lower(unaccent(:code))";

    return await _session.CreateQuery(hql)
        .SetParameter("code", code)
        .UniqueResultAsync<TechnicalStandard?>();
}
```

---

## Complete Examples

### Example 1: Read-Only Repository (DAO)

```csharp
using {project}.domain.daos;
using {project}.domain.interfaces.repositories;
using NHibernate;

namespace {project}.infrastructure.nhibernate;

/// <summary>
/// Read-only repository for OrderDao (no modifications allowed).
/// </summary>
public class NHOrderDaoRepository(ISession session)
    : NHReadOnlyRepository<OrderDao, Guid>(session), IReadOnlyRepository<OrderDao, Guid>
{
    // Inherits all read methods from NHReadOnlyRepository
    // No write methods (Add, Save, Delete) available
}
```

### Example 2: Full CRUD Repository (User)

```csharp
using {project}.domain.entities;
using {project}.domain.exceptions;
using {project}.domain.interfaces.repositories;
using NHibernate;
using NHibernate.Linq;

namespace {project}.infrastructure.nhibernate;

public class NHUserRepository(ISession session, IServiceProvider serviceProvider)
    : NHRepository<User, Guid>(session, serviceProvider), IUserRepository
{
    public async Task<User> CreateAsync(string email, string name)
    {
        var user = new User(email, name);

        if (!user.IsValid())
            throw new InvalidDomainException(user.Validate());

        var existing = await GetByEmailAsync(email);
        if (existing != null)
            throw new DuplicatedDomainException($"A user with email '{email}' already exists.");

        await AddAsync(user);
        this.FlushWhenNotActiveTransaction();
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _session.Query<User>()
            .Where(u => u.Email.ToLowerInvariant() == email.ToLowerInvariant())
            .SingleOrDefaultAsync();
    }

    public async Task<User> UpdateNameAsync(Guid id, string newName)
    {
        var user = await GetAsync(id);
        if (user == null)
            throw new ResourceNotFoundException($"User with id '{id}' not found.");

        user.Name = newName;

        if (!user.IsValid())
            throw new InvalidDomainException(user.Validate());

        await _session.UpdateAsync(user);
        this.FlushWhenNotActiveTransaction();
        return user;
    }
}
```

### Example 3: CRUD Repository (TechnicalStandard with HQL)

```csharp
public class NHTechnicalStandardRepository(ISession session, IServiceProvider serviceProvider)
    : NHRepository<TechnicalStandard, Guid>(session, serviceProvider), ITechnicalStandardRepository
{
    public async Task<TechnicalStandard> CreateAsync(
        string code, string name, string edition, string status, string type)
    {
        var entity = new TechnicalStandard(code, name, edition, status, type);
        if (!entity.IsValid())
            throw new InvalidDomainException(entity.Validate());

        var existing = await GetByCodeAsync(code);
        if (existing != null)
            throw new DuplicatedDomainException($"A technical standard with code '{code}' already exists.");

        await AddAsync(entity);
        this.FlushWhenNotActiveTransaction();
        return entity;
    }

    public async Task<TechnicalStandard?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var hql = @"
            from TechnicalStandard ts
            where lower(unaccent(ts.Code)) = lower(unaccent(:code))";

        return await _session.CreateQuery(hql)
            .SetParameter("code", code)
            .UniqueResultAsync<TechnicalStandard?>();
    }

    public async Task<TechnicalStandard> UpdateAsync(
        Guid id, string code, string name, string edition, string status, string type)
    {
        var entity = await _session.Query<TechnicalStandard>()
            .Where(ts => ts.Id == id)
            .SingleOrDefaultAsync();

        if (entity == null)
            throw new ResourceNotFoundException($"Technical standard with id '{id}' does not exist.");

        var existingWithCode = await GetByCodeAsync(code);
        if (existingWithCode != null && existingWithCode.Id != id)
            throw new DuplicatedDomainException($"A technical standard with code '{code}' already exists.");

        entity.Code = code;
        entity.Name = name;
        entity.Edition = edition;
        entity.Status = status;
        entity.Type = type;

        if (!entity.IsValid())
            throw new InvalidDomainException(entity.Validate());

        await _session.UpdateAsync(entity);
        this.FlushWhenNotActiveTransaction();
        return entity;
    }

    public async Task<TechnicalStandard?> GetByIdAsync(Guid id)
    {
        return await _session.Query<TechnicalStandard>()
            .Where(ts => ts.Id == id)
            .SingleOrDefaultAsync();
    }
}
```
