# AutoMapper Profiles Reference

## Profile Structure

Each entity gets its own `{Entity}MappingProfile` class that inherits from `AutoMapper.Profile`:

```csharp
using AutoMapper;
using {project}.domain.entities;
using {project}.domain.daos;
using {project}.webapi.dtos;
using {project}.webapi.features.{feature}.models;
using {project}.application.usecases.{feature};

namespace {project}.webapi.mappingprofiles;

/// <summary>
/// Mapping profile for {Entity} entity and {Entity}Dto.
/// </summary>
public class {Entity}MappingProfile : Profile
{
    public {Entity}MappingProfile()
    {
        // 1. Entity -> DTO
        CreateMap<{Entity}, {Entity}Dto>();

        // 2. DAO -> DTO (for GetManyAndCount queries)
        CreateMap<{Entity}Dao, {Entity}Dto>();

        // 3. Request -> Command (one per operation)
        CreateMap<Create{Entity}Model.Request, Create{Entity}UseCase.Command>();
        CreateMap<Update{Entity}Model.Request, Update{Entity}UseCase.Command>();

        // 4. Entity -> Response (via ForMember nesting DTO)
        CreateMap<{Entity}, Create{Entity}Model.Response>()
            .ForMember(dest => dest.{Entity}, opt => opt.MapFrom(src => src));
        CreateMap<{Entity}, Get{Entity}Model.Response>()
            .ForMember(dest => dest.{Entity}, opt => opt.MapFrom(src => src));
        CreateMap<{Entity}, Update{Entity}Model.Response>()
            .ForMember(dest => dest.{Entity}, opt => opt.MapFrom(src => src));

        // 5. DAO paginated -> DTO paginated (for GetManyAndCount)
        CreateMap<GetManyAndCountResultDto<{Entity}Dao>,
                  GetManyAndCountResultDto<{Entity}Dto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));
    }
}
```

## 4 Required Mapping Types

Every entity CRUD set needs these mappings:

| # | Direction | Purpose | Example |
|---|-----------|---------|---------|
| 1 | Entity -> DTO | Outbound data for API | `CreateMap<User, UserDto>()` |
| 2 | DAO -> DTO | Optimized read queries | `CreateMap<UserDao, UserDto>()` |
| 3 | Request -> Command | Inbound data to use case | `CreateMap<CreateUserModel.Request, CreateUserUseCase.Command>()` |
| 4 | Entity -> Response | Wrap DTO in response | `CreateMap<User, CreateUserModel.Response>().ForMember(...)` |

### Entity -> Response Pattern

The Response wraps a DTO. Use `ForMember` to map the entity into the DTO property:

```csharp
CreateMap<TechnicalStandard, CreateTechnicalStandardModel.Response>()
    .ForMember(dest => dest.TechnicalStandard, opt => opt.MapFrom(src => src));
// AutoMapper: src (TechnicalStandard) -> TechnicalStandardDto (via existing map) -> Response.TechnicalStandard
```

This works because AutoMapper chains: it sees `src` is `TechnicalStandard` and `dest.TechnicalStandard` is `TechnicalStandardDto`, so it uses the `CreateMap<TechnicalStandard, TechnicalStandardDto>()` mapping.

### Collection Transformation (Roles Example)

When entity has complex relations, transform in the Entity -> DTO map:

```csharp
CreateMap<User, UserDto>()
    .ForMember(dest => dest.Roles,
        opt => opt.MapFrom(src => src.Roles.Select(r => r.Name)));
// IList<Role> -> IEnumerable<string>
```

## Generic MappingProfile (GetManyAndCountResult)

The shared `MappingProfile.cs` maps the domain `GetManyAndCountResult<T>` to the DTO:

```csharp
using AutoMapper;
using {project}.domain.interfaces.repositories;
using {project}.webapi.dtos;

namespace {project}.webapi.mappingprofiles;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Generic mapping from GetManyAndCountResult<T> to GetManyAndCountResultDto<T>
        CreateMap(typeof(GetManyAndCountResult<>), typeof(GetManyAndCountResultDto<>))
            .ForMember(nameof(GetManyAndCountResultDto<object>.SortBy),
                opt => opt.MapFrom((src, _, __, ___) =>
                {
                    return (src as IGetManyAndCountResultWithSorting)?.Sorting.SortBy;
                }))
            .ForMember(nameof(GetManyAndCountResultDto<object>.SortCriteria),
                opt => opt.MapFrom((src, _, __, ___) =>
                {
                    return (src as IGetManyAndCountResultWithSorting)?.Sorting.Criteria switch
                    {
                        SortingCriteriaType.Ascending => "asc",
                        SortingCriteriaType.Descending => "desc",
                        _ => null
                    };
                }));
    }
}
```

This is created **once per project** and handles all `GetManyAndCount` operations generically.

## Complete Example: TechnicalStandardMappingProfile

```csharp
using AutoMapper;
using {project}.application.usecases.technicalstandards;
using {project}.domain.entities;
using {project}.webapi.dtos;
using {project}.webapi.features.technicalstandards.models;

namespace {project}.webapi.mappingprofiles;

/// <summary>
/// Mapping profile for TechnicalStandard entity and TechnicalStandardDto.
/// </summary>
public class TechnicalStandardMappingProfile : Profile
{
    public TechnicalStandardMappingProfile()
    {
        // Entity -> DTO
        CreateMap<TechnicalStandard, TechnicalStandardDto>();

        // DAO -> DTO
        CreateMap<TechnicalStandardDao, TechnicalStandardDto>();

        // Entity -> Response
        CreateMap<TechnicalStandard, GetTechnicalStandardModel.Response>()
            .ForMember(dest => dest.TechnicalStandard, opt => opt.MapFrom(src => src));
        CreateMap<TechnicalStandard, CreateTechnicalStandardModel.Response>()
            .ForMember(dest => dest.TechnicalStandard, opt => opt.MapFrom(src => src));
        CreateMap<TechnicalStandard, UpdateTechnicalStandardModel.Response>()
            .ForMember(dest => dest.TechnicalStandard, opt => opt.MapFrom(src => src));

        // DAO paginated -> DTO paginated
        CreateMap<GetManyAndCountResultDto<TechnicalStandardDao>,
                  GetManyAndCountResultDto<TechnicalStandardDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count))
            .ForMember(dest => dest.PageNumber, opt => opt.MapFrom(src => src.PageNumber))
            .ForMember(dest => dest.PageSize, opt => opt.MapFrom(src => src.PageSize))
            .ForMember(dest => dest.SortBy, opt => opt.MapFrom(src => src.SortBy))
            .ForMember(dest => dest.SortCriteria, opt => opt.MapFrom(src => src.SortCriteria));

        // Request -> Command
        CreateMap<CreateTechnicalStandardModel.Request, CreateTechnicalStandardUseCase.Command>();
        CreateMap<UpdateTechnicalStandardModel.Request, UpdateTechnicalStandardUseCase.Command>();
    }
}
```

## DI Registration

AutoMapper profiles are auto-discovered via assembly scanning in `Program.cs`:

```csharp
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

No manual registration per profile is needed.

## Injection in Endpoints

```csharp
public class CreateUserEndpoint(AutoMapper.IMapper mapper)
    : BaseEndpoint<CreateUserModel.Request, CreateUserModel.Response>
{
    private readonly AutoMapper.IMapper _mapper = mapper;
}
```

Always inject `AutoMapper.IMapper` (fully qualified to avoid ambiguity).

## File Location

```
src/{project}.webapi/
└── mappingprofiles/
    ├── MappingProfile.cs                        <- Generic (one per project)
    ├── UserMappingProfile.cs                    <- Per entity
    ├── TechnicalStandardMappingProfile.cs
    └── PrototypeMappingProfile.cs
```

## Anti-Patterns

```csharp
// NEVER: Use ReverseMap (unidirectional mappings in this architecture)
CreateMap<User, UserDto>().ReverseMap();

// NEVER: Map entities directly in API without DTO
var response = mapper.Map<User>(request);

// NEVER: Put business logic in mapping
.AfterMap((src, dest) => { dest.Total = CalculateTotal(src); });

// NEVER: Manual mapping in endpoint when AutoMapper is available
var dto = new UserDto { Id = user.Id, Name = user.Name };
```
