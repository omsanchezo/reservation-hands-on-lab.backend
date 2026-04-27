# Date Handling

> **"Store UTC, Display Local"** - The backend ALWAYS stores and processes dates in UTC. Conversion to local time is the frontend's responsibility.

## The Problem

When the frontend sends a date without timezone, the backend interprets it incorrectly:

```
Frontend sends:     "2026-01-22T06:00:00"     (6 AM, no timezone)
Backend receives:   DateTime with Kind = Unspecified
Backend compares:   DateTime.UtcNow           (Kind = UTC)
Result:             INCORRECT comparison - mixes dates without timezone with UTC
```

## DateTimeOffset vs DateTime

### Option 1: DateTimeOffset (Recommended)

`DateTimeOffset` captures date + offset, eliminating ambiguity:

```csharp
public class CreateFollowUpModel
{
    public class Request
    {
        /// <summary>
        /// Scheduled date with timezone offset.
        /// Expected format: ISO 8601 with offset (e.g., "2026-01-22T06:00:00-06:00")
        /// </summary>
        public DateTimeOffset ScheduledDate { get; set; }
    }
}
```

### Option 2: DateTime with Explicit Conversion

If you keep `DateTime`, conversion to UTC must be explicit in the mapping or use case.

## ToUtcSafe Helper

When `DateTimeOffset` is not an option, use a safe conversion helper:

```csharp
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a DateTime to UTC safely.
    /// If Kind is Unspecified, assumes the datetime is already in UTC.
    /// </summary>
    public static DateTime ToUtcSafe(this DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => dateTime
        };
    }
}

// Usage in mapping
.ForMember(dest => dest.ScheduledDate,
    opt => opt.MapFrom(src => src.ScheduledDate.ToUtcSafe()));
```

## AutoMapper Conversion

```csharp
public class FollowUpMappingProfile : Profile
{
    public FollowUpMappingProfile()
    {
        // DateTimeOffset -> DateTime UTC
        CreateMap<CreateFollowUpModel.Request, CreateFollowUpUseCase.Command>()
            .ForMember(dest => dest.ScheduledDate,
                opt => opt.MapFrom(src => src.ScheduledDate.UtcDateTime));
    }
}
```

## Entity Storage: Always UTC

```csharp
public class FollowUp : AbstractDomainObject
{
    /// <summary>
    /// Scheduled date in UTC.
    /// </summary>
    public virtual DateTime ScheduledDate { get; set; }

    public FollowUp(DateTime scheduledDateUtc, string notes)
    {
        ScheduledDate = scheduledDateUtc;
        Notes = notes;
    }

    public virtual bool IsOverdue()
        => Status == FollowUpStatus.Pending && ScheduledDate.Date < DateTime.UtcNow.Date;

    public virtual bool IsDueToday()
        => Status == FollowUpStatus.Pending && ScheduledDate.Date == DateTime.UtcNow.Date;
}
```

## Comparisons: Always DateTime.UtcNow

```csharp
// GOOD
public virtual bool IsOverdue()
    => ScheduledDate.Date < DateTime.UtcNow.Date;

// BAD - DateTime.Now uses server local time
public virtual bool IsOverdue()
    => ScheduledDate.Date < DateTime.Now.Date;
```

## Repository Queries

```csharp
public async Task<IEnumerable<FollowUp>> GetOverdueFollowUpsAsync()
{
    var today = DateTime.UtcNow.Date;
    return await _session.Query<FollowUp>()
        .Where(f => f.Status == FollowUpStatus.Pending)
        .Where(f => f.ScheduledDate.Date < today)
        .ToListAsync();
}

public async Task<IEnumerable<FollowUp>> GetDueTodayAsync()
{
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    return await _session.Query<FollowUp>()
        .Where(f => f.Status == FollowUpStatus.Pending)
        .Where(f => f.ScheduledDate >= today && f.ScheduledDate < tomorrow)
        .ToListAsync();
}
```

## DTOs: Return UTC with Z Suffix

```csharp
public class FollowUpDto
{
    /// <summary>
    /// Scheduled date in UTC (ISO 8601 with Z suffix).
    /// </summary>
    public DateTime ScheduledDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
// JSON output: { "scheduledDate": "2026-01-22T12:00:00Z" }
```

## Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| `DateTime.Now` in comparisons | Uses server local time | Use `DateTime.UtcNow` |
| `DateTime` without Kind | Ambiguous, server-dependent | Use `DateTimeOffset` or specify Kind |
| Store local time | Inconsistent across servers | Always store UTC |
| Compare different Kinds | Incorrect results | Normalize everything to UTC |
| `.ToUniversalTime()` on Unspecified | Assumes Local incorrectly | Use safe helper or `DateTimeOffset` |
| Format dates in backend | Locale-dependent | Return UTC, format in frontend |

```csharp
// BAD
ScheduledDate = DateTime.Now;
if (inputDate > existingDate) // Both may have different Kind
var utc = unspecifiedDateTime.ToUniversalTime(); // Server-dependent

// GOOD
ScheduledDate = DateTime.UtcNow;
public DateTimeOffset ScheduledDate { get; set; }
var utc = inputDateTime.ToUtcSafe();
```

## Checklist

- [ ] Request models use `DateTimeOffset` for date input properties
- [ ] AutoMapper converts `DateTimeOffset` to UTC with `.UtcDateTime`
- [ ] Entity `DateTime` properties store UTC
- [ ] Constructors document they expect UTC
- [ ] Comparisons use `DateTime.UtcNow`
- [ ] Repository queries use `DateTime.UtcNow` as reference
- [ ] DTOs serialize with `Z` suffix automatically
- [ ] Future-date validators compare against UTC
