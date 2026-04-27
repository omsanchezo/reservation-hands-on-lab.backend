# Value Objects Reference

Value Objects are immutable, equality by value, no identity, self-validating.

- Implement as `sealed record` with `init` properties in C# 13
- Constructor validates and throws `ArgumentException` on invalid input
- Do NOT inherit `AbstractDomainObject`
- Common examples: Email, Money, Address, DateRange, PhoneNumber

## Complete Pattern

```csharp
public sealed record Email
{
    public string Value { get; init; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format");
        Value = value;
    }
}
```

## Money Example

```csharp
public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required");
        Amount = amount;
        Currency = currency;
    }
}
```

## DateRange Example

```csharp
public sealed record DateRange
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }

    public DateRange(DateTime start, DateTime end)
    {
        if (end <= start) throw new ArgumentException("End must be after Start");
        Start = start;
        End = end;
    }
}
```

## When to Use

- Types defined by their value, with invariants/rules

## When NOT to Use

- Simple single property without validation rules -- use primitive directly

## NHibernate Integration

Via Component mapping or Custom User Types.

## Checklist

- [ ] sealed record
- [ ] init properties (not set)
- [ ] Constructor validates all invariants
- [ ] No AbstractDomainObject inheritance
- [ ] Throws ArgumentException on invalid input
