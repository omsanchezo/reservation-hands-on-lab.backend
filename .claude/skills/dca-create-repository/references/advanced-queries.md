# Advanced Queries

NHibernate query techniques: LINQ, HQL, QueryOver, Dynamic LINQ, pagination, filtering, and quick search.

---

## Query Methods Overview

| Method | Use Case |
|--------|----------|
| **LINQ to NHibernate** | Type-safe, compile-time queries |
| **HQL** | DB-specific functions (unaccent), complex joins |
| **Dynamic LINQ** | Runtime queries from REST API query strings |
| **QueryOver** | Type-safe alternative for complex queries |

```csharp
using NHibernate.Linq;           // LINQ to NHibernate
using System.Linq.Dynamic.Core;  // Dynamic LINQ
using NHibernate;                // HQL, QueryOver
```

---

## LINQ to NHibernate

```csharp
// Get all
public IEnumerable<T> Get() => this._session.Query<T>();

// Filter with Where
public async Task<User?> GetByEmailAsync(string email)
{
    return await _session.Query<User>()
        .Where(u => u.Email == email)
        .SingleOrDefaultAsync();
}

// Filter with Expression
public IEnumerable<T> Get(Expression<Func<T, bool>> query)
    => this._session.Query<T>().Where(query);

// Pagination with Dynamic LINQ ordering
public IEnumerable<T> Get(Expression<Func<T, bool>> query, int page, int pageSize, SortingCriteria sortingCriteria)
    => this._session.Query<T>()
            .Where(query)
            .OrderBy(sortingCriteria.ToExpression())
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
```

---

## HQL (Hibernate Query Language)

Use HQL when you need DB-specific functions like `unaccent()` or `lower()`.

```csharp
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
```

Always use named parameters to prevent SQL injection:

```csharp
// CORRECT
var hql = "from User u where u.Email = :email";
var user = await _session.CreateQuery(hql)
    .SetParameter("email", email)
    .UniqueResultAsync<User?>();

// WRONG - SQL Injection vulnerability
var hql = $"from User u where u.Email = '{email}'";
```

---

## QueryOver API

```csharp
int total = _session.QueryOver<User>().RowCount();

var users = _session.QueryOver<User>()
    .Where(u => u.Email == email)
    .List();

var page = _session.QueryOver<User>()
    .Skip(10)
    .Take(25)
    .List();
```

---

## Dynamic LINQ

Runtime ordering from query string values:

```csharp
public static string ToExpression(this SortingCriteria sort)
{
    string orderExpression = sort.Criteria == SortingCriteriaType.Ascending
        ? $"{sort.SortBy}"
        : $"{sort.SortBy} descending";
    return orderExpression;
}

// Usage
var results = _session.Query<User>()
    .OrderBy($"{sortBy} {direction}")  // Dynamic LINQ
    .ToList();
```

---

## Query String Parsing Architecture

```
HTTP Request (pageNumber, pageSize, sortBy, sortDirection, filters, query)
    --> QueryStringParser (parses pagination, sorting, filter operators, quick search)
    --> FilterExpressionParser (builds Expression<Func<T, bool>>)
    --> LINQ to NHibernate (.Where(expression).OrderBy(...).Skip(...).Take(...))
```

### Query String Format

```
GET /api/users?pageNumber=2&pageSize=25&sortBy=Name&sortDirection=desc&Email=john@example.com||eq&Age=18|65||between&query=search text||Name|Email
```

### QueryStringParser

```csharp
public const int DEFAULT_PAGE_NUMBER = 1;
public const int DEFAULT_PAGE_SIZE = 25;

public int ParsePageNumber() { /* validates and returns pageNumber from query string */ }

public Sorting ParseSorting<T>(string defaultFieldName)
{
    // Validates sortBy property exists on type T via reflection
    PropertyInfo[] properties = typeof(T).GetProperties();
    if (!properties.Any(p => p.Name.Equals(sortByValue, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidQueryStringArgumentException("sortBy");
    return new Sorting(sortByField, sortDirection);
}

public IList<FilterOperator> ParseFilterOperators<T>()
{
    // Parses "value1|value2||operator" format from non-reserved query params
    string[] filterData = filter.Value.Split("||");
    string[] filterValues = filterData[0].Split("|");
    var filterOperator = filterData[1];
    // ...
}
```

---

## FilterExpressionParser

Builds LINQ expressions dynamically from filter operators:

```csharp
static public Expression<Func<T, bool>> ParsePredicate<T>(IEnumerable<FilterOperator> operands)
{
    var parameterExpression = Expression.Parameter(typeof(T), nameof(T).ToLower());
    List<Expression> allCriteria = new List<Expression>();

    foreach (FilterOperator filter in operands)
    {
        string propertyName = filter.FieldName.ToPascalCase();
        Expression propertyExpression = Expression.Property(parameterExpression, propertyName);

        switch (filter.RelationalOperatorType)
        {
            case RelationalOperator.Contains:
                propertyExpression = CallToStringMethod<T>(propertyExpression, propertyName);
                var constant = Expression.Constant(filterValues.FirstOrDefault());
                MethodInfo? strContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                if (strContainsMethod != null)
                    criteria = Expression.Call(propertyExpression, strContainsMethod, new Expression[] { constant });
                break;

            case RelationalOperator.GreaterThan:
                var constantExpression = CreateConstantExpression<T>(propertyName, filterValues[0]);
                criteria = Expression.GreaterThan(propertyExpression, constantExpression);
                break;

            case RelationalOperator.Between:
                var lowerLimit = CreateConstantExpression<T>(propertyName, filterValues[0]);
                var upperLimit = CreateConstantExpression<T>(propertyName, filterValues[1]);
                criteria = Expression.AndAlso(
                    Expression.GreaterThanOrEqual(propertyExpression, lowerLimit),
                    Expression.LessThanOrEqual(propertyExpression, upperLimit));
                break;

            default: // Equal - uses array Contains for multi-value equality
                propertyExpression = CallToStringMethod<T>(propertyExpression, propertyName);
                criteria = Expression.Call(Expression.Constant(filterValues), arrContainsMethod, propertyExpression);
                break;
        }
        if (criteria != null) allCriteria.Add(criteria);
    }

    // Combine all criteria with AND
    Expression? expression = null;
    foreach (Expression criteria in allCriteria)
        expression = expression != null ? Expression.AndAlso(expression, criteria) : criteria;

    return Expression.Lambda<Func<T, bool>>(expression!, parameterExpression);
}
```

### Type Conversion

```csharp
private static Expression CreateConstantExpression<T>(string propertyName, string constantValue)
{
    PropertyInfo? propertyInfo = typeof(T).GetProperty(propertyName);
    var actualType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

    if (actualType == typeof(DateTime))
    {
        if (DateTime.TryParseExact(constantValue, new[] { "yyyy-MM-dd" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
        {
            dateValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Utc);
            return Expression.Constant(dateValue, propertyInfo.PropertyType);
        }
        throw new InvalidFilterArgumentException($"Invalid date format. Use yyyy-MM-dd");
    }

    object convertedValue = actualType.IsEnum
        ? Enum.Parse(actualType, constantValue)
        : Convert.ChangeType(constantValue, actualType);
    return Expression.Constant(convertedValue, propertyInfo.PropertyType);
}
```

---

## GetManyAndCount Pattern

```csharp
public class GetManyAndCountResult<T>
{
    public IEnumerable<T> Items { get; set; }
    public long Count { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public SortingCriteria Sorting { get; set; }
}

public async Task<GetManyAndCountResult<T>> GetManyAndCountAsync(
    string? query, string defaultSorting, CancellationToken ct = default)
{
    var (expression, pageNumber, pageSize, sortingCriteria) = PrepareQuery(query, defaultSorting);

    // Execute queries SEQUENTIALLY to avoid DataReader conversion issues
    var total = await this._session.Query<T>()
        .Where(expression).CountAsync(ct);

    var items = await this._session.Query<T>()
        .OrderBy(sortingCriteria.ToExpression())
        .Where(expression)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    return new GetManyAndCountResult<T>(items, total, pageNumber, pageSize, sortingCriteria);
}
```

---

## Filter Operators

| Operator | Query String | SQL Generated |
|----------|-------------|---------------|
| `equal` | `Status=active\|\|eq` | `WHERE status = 'active'` |
| `contains` | `Name=john\|\|contains` | `WHERE name LIKE '%john%'` |
| `greater_than` | `Age=18\|\|gt` | `WHERE age > 18` |
| `greater_or_equal_than` | `Price=100\|\|gte` | `WHERE price >= 100` |
| `less_than` | `Stock=10\|\|lt` | `WHERE stock < 10` |
| `less_or_equal_than` | `Discount=50\|\|lte` | `WHERE discount <= 50` |
| `between` | `Age=18\|65\|\|between` | `WHERE age >= 18 AND age <= 65` |

Multiple filters are combined with AND:

```
GET /api/users?Email=john||contains&Age=18||gt&Status=active||eq
// => u.Email.Contains("john") && u.Age > 18 && u.Status == "active"
```

---

## Quick Search

Search across multiple fields simultaneously using OR logic.

```csharp
// Search ALL string fields: GET /api/users?query=john
// Search SPECIFIC fields:   GET /api/users?query=john||Name|Email

public QuickSearch? ParseQuery<T>()
{
    // Case 1: No field specifier -> search all string/int fields (except Id)
    // Case 2: Fields after || -> search only those fields
}
```

Generated expression combines with existing filters:
- Quick search fields are joined with OR
- Quick search result is ANDed with other filter expressions

---

## Anti-Patterns

**ToList() before filtering** -- loads entire table into memory:

```csharp
// WRONG
var users = _session.Query<User>().ToList().Where(u => u.Age > 18).ToList();

// CORRECT - filter executes in database
var users = _session.Query<User>().Where(u => u.Age > 18).ToList();
```

**N+1 query problem** -- use Fetch for eager loading:

```csharp
// WRONG: 101 queries for 100 users
var users = _session.Query<User>().ToList();
foreach (var user in users) { var roles = user.Roles.ToList(); }

// CORRECT: 1 query with JOIN
var users = _session.Query<User>().Fetch(u => u.Roles).ToList();
```

**Parallel async queries on same session** -- NHibernate DataReader issues:

```csharp
// WRONG
var totalTask = _session.Query<T>().Where(expression).CountAsync(ct);
var itemsTask = _session.Query<T>().Where(expression).ToListAsync(ct);
await Task.WhenAll(totalTask, itemsTask);

// CORRECT - sequential execution
var total = await _session.Query<T>().Where(expression).CountAsync(ct);
var items = await _session.Query<T>().Where(expression).ToListAsync(ct);
```
