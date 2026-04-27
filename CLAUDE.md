# CLAUDE.md — reservation.handson.lab.backend

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Backend API for a **reservation / booking** hands-on lab. The solution is the seed of a reservation platform (customers, reservable resources, bookings) used to exercise the APSYS .NET Clean Architecture workflow end-to-end through OpenSpec.

## Project status

**Early-bootstrap.** The solution skeleton, the Domain base abstractions, and the test projects exist; `Application`, `Infrastructure`, and `WebApi` (currently a `Hello World!` minimal API) have no feature code yet. The runtime stack listed below is the **intended** target — packages are wired in per-feature when the corresponding skill is invoked, not preloaded.

New features are added through the OpenSpec workflow described below.

---

## Tech stack

| Layer / concern | Component | Status |
| --- | --- | --- |
| Runtime / language | .NET 9, C# 13 | wired |
| WebApi | FastEndpoints 7.0 | intended (not yet wired) |
| ORM / DB | NHibernate 5.5 + PostgreSQL (Npgsql) | intended |
| Validation | FluentValidation | wired in Domain |
| Mapping | AutoMapper 15 | intended |
| Migrations | FluentMigrator 7.1 | intended |
| Error handling | FluentResults | intended (Application) |
| Testing framework | NUnit 4 | wired |
| Assertions / mocks | FluentAssertions 8, AutoFixture.AutoMoq, Castle.Core | wired |
| Endpoint integration | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) | wired |
| Coverage | Coverlet collector | wired |

The intended runtime stack is sourced from `openspec/schemas/dotnet-clean-architecture/example-config.yaml`.

---

## Commands

Run from the backend root (the directory containing the `.sln`):

| Task | Command |
| --- | --- |
| Build everything | `dotnet build` |
| Run all tests | `dotnet test` |
| Run a single test project | `dotnet test tests/reservation.handson.lab.backend.domain.tests` |
| Run a single test by name | `dotnet test --filter "FullyQualifiedName~MyTestClass.MyTest"` |
| Run the WebApi | `dotnet run --project src/reservation.handson.lab.backend.webapi` |
| Restore | `dotnet restore` |

Notes:
- Add packages with `dotnet add <project> package <Name>` (no `--version`), then **pin the version in `Directory.Packages.props`** — see "Central package management" below.
- `dotnet new nunit` emits `Version="..."` on `PackageReference`, which is incompatible with central package management (NU1008). After scaffolding, strip the `Version` attribute from each `PackageReference` and add the version to `Directory.Packages.props`.

---

## Architecture

Strict Clean Architecture layering. **Dependencies only flow inward** — outer layers reference inner ones, never the reverse.

| Project | May reference | Must NOT reference |
| --- | --- | --- |
| `reservation.handson.lab.backend.domain` | nobody (only `FluentValidation` external) | Application, Infrastructure, WebApi |
| `reservation.handson.lab.backend.application` | Domain | Infrastructure, WebApi |
| `reservation.handson.lab.backend.infrastructure` | Domain | Application, WebApi |
| `reservation.handson.lab.backend.webapi` | Domain, Application, Infrastructure (DI only) | — |

Each `src/*` project has a sibling `tests/*` project that follows the same layering.

### Project structure (current)

```
src/
├── reservation.handson.lab.backend.domain/
│   ├── entities/
│   │   ├── AbstractDomainObject.cs
│   │   └── validators/
│   ├── exceptions/
│   │   ├── DuplicatedDomainException.cs
│   │   ├── InvalidDomainException.cs
│   │   └── ResourceNotFoundException.cs
│   └── interfaces/
│       └── repositories/
│           ├── GetManyAndCountResult.cs
│           ├── IReadOnlyRepository.cs
│           ├── IRepository.cs
│           ├── IUnitOfWork.cs
│           └── SortingCriteria.cs
├── reservation.handson.lab.backend.application/   # empty (use cases land here)
├── reservation.handson.lab.backend.infrastructure/ # empty (NHibernate / DI lands here)
└── reservation.handson.lab.backend.webapi/         # Hello World minimal API
tests/
├── reservation.handson.lab.backend.domain.tests/
├── reservation.handson.lab.backend.application.tests/
├── reservation.handson.lab.backend.infrastructure.tests/
└── reservation.handson.lab.backend.webapi.tests/
```

There is no `migrations` project yet — it is added when the first migration task is implemented.

### Layer responsibilities (per OpenSpec config)

- **Domain** — entities (inheriting `AbstractDomainObject`), FluentValidation validators in `entities/validators/`, value objects, domain exceptions (`InvalidDomainException`, `DuplicatedDomainException`, `ResourceNotFoundException`), DAOs, repository interfaces (`IRepository<T,TKey>`, `IReadOnlyRepository<T,TKey>`, `IUnitOfWork`).
- **Application** — use cases as Command + Handler per operation, `FluentResults` for error returns, transaction management via `IUnitOfWork`.
- **Infrastructure** — NHibernate `ClassMapping`s, repository implementations, Unit of Work, DI registration, HTTP clients, caching.
- **WebApi** — FastEndpoints endpoints, request/response DTOs, AutoMapper profiles. Domain exceptions are mapped to HTTP status codes here, not in the use cases.

### Domain conventions already in place

- `AbstractDomainObject` (`domain/entities/AbstractDomainObject.cs`) is the base for all entities. It exposes `virtual Id : Guid` and `virtual CreationDate : DateTime`. **Keep properties `virtual`** so NHibernate proxying works once Infrastructure is wired.
- `IsValid()` / `Validate()` look up an `IValidator` via the overridable `GetValidator()` hook — every entity creates a validator under `entities/validators/` and overrides `GetValidator()` to return it.
- Repositories are generic on `<T, TKey>`. The standard pattern is `IRepository<MyEntity, Guid>` (or `IReadOnlyRepository<...>` for query-only).
- Paginated queries return `GetManyAndCountResult<T>` (default page size 25) with a `SortingCriteria`.
- `IUnitOfWork` is the **single coordinator** — when a new aggregate is added, expose its repository as a property here (e.g. `IReservationRepository Reservations { get; }`), don't inject repositories independently.
- Namespaces are lowercase-dotted and mirror the folder path (`reservation.handson.lab.backend.domain.interfaces.repositories`). Match this style when adding files.

---

## Naming

| Type | Pattern | Example |
| --- | --- | --- |
| Entity | `{Entity}.cs` | `Reservation.cs` |
| Validator | `{Entity}Validator.cs` | `ReservationValidator.cs` |
| DAO | `{Entity}Dao.cs` | `ReservationDao.cs` |
| Repository interface | `I{Entity}Repository.cs` | `IReservationRepository.cs` |
| NHibernate repository | `NH{Entity}Repository.cs` | `NHReservationRepository.cs` |
| Entity mapper (NHibernate) | `{Entity}Mapper.cs` | `ReservationMapper.cs` |
| DAO mapper | `{Entity}DaoMapper.cs` | `ReservationDaoMapper.cs` |
| Use case (handler) | `{Action}{Entity}UseCase.cs` | `CreateReservationUseCase.cs` |
| Endpoint | `{Action}{Entity}Endpoint.cs` | `CreateReservationEndpoint.cs` |
| Endpoint model | `{Action}{Entity}Model.cs` | `CreateReservationModel.cs` |
| DTO | `{Entity}Dto.cs` | `ReservationDto.cs` |
| Migration | `M{NNN}{Description}.cs` | `M001CreateReservationsTable.cs` |

**Style:** PascalCase for classes/methods, `_camelCase` for private fields, camelCase for parameters/locals, kebab-case for folders, snake_case for DB tables/columns.

---

## Language rules

- Code, comments, commit messages: **English**.
- Identifiers (class / method / property names): **English**.
- Error messages returned to API clients: **English** (centralize in resource files when introduced).
- Conversation with the user, OpenSpec proposals/specs/design notes: **Spanish** is fine — match the user's language.

---

## Central package management

`Directory.Packages.props` at the repo root manages **all** package versions (`ManagePackageVersionsCentrally=true`). Rules:

- `PackageReference` entries in `.csproj` files must NOT contain a `Version` attribute.
- To add a dependency: add a `<PackageVersion Include="..." Version="..." />` to `Directory.Packages.props`, then a `<PackageReference Include="..." />` (no version) in the consuming project.
- `CentralPackageTransitivePinningEnabled=false` — transitive dependencies stay floating; only direct references are pinned.

---

## OpenSpec workflow

This repo uses an OpenSpec schema (`openspec/schemas/dotnet-clean-architecture/`) to drive feature work through a five-stage cycle: **proposal → specs → design → tasks → apply**.

- **Proposal** — business `Why` / `What`, no technical notation. Names features in kebab-case, grouped by domain feature (`customer-management`, `reservation-booking`), not by layer.
- **Specs** — `specs/<feature>/spec.md` per feature, observable behavior only (`SHALL`/`MUST`), every operation has happy-path + validation + not-found + duplicate scenarios. No mappings, no transactions, no DAOs in here.
- **Design** — only when there are cross-cutting concerns or non-trivial decisions. Captures NHibernate strategy, endpoint design, error-mapping strategy, migration strategy, etc.
- **Tasks** — strictly ordered by layer dependency: `migrations → domain → repositories → infrastructure → application → webapi → tests`. Each task references one of the `dca-*` skills below.
- **Apply** — execute tasks in order, loading the referenced skill each time. **Do not skip layers or reorder.**

### Custom skills (`.claude/skills/dca-*`)

Per-task implementation guides for this stack — invoke the matching skill before writing code in that layer:

| Skill | When to use |
| --- | --- |
| `dca-create-domain` | New entity / validator / value object / domain exception / DAO / repository interface |
| `dca-create-migration` | FluentMigrator migrations (tables, columns, indexes, FKs, views, seed data) |
| `dca-create-repository` | NHibernate `ClassMapping`, `NHRepository`/`NHReadOnlyRepository`, UoW implementation, session factory |
| `dca-create-infrastructure` | DI registration, `IHttpClientFactory` clients, Redis/`IMemoryCache`, other cross-cutting services |
| `dca-create-use-case` | Application Command + Handler with `FluentResults` and transaction management |
| `dca-create-endpoint` | FastEndpoints class, request/response DTOs, AutoMapper profile, domain→HTTP error mapping |
| `dca-create-tests` | Unit (domain), handler (application), NHibernate integration (infrastructure), endpoint (webapi) tests |
| `dca-review-pr-code` | Generate a structured PR review report against APSYS Clean Architecture standards |

Each skill carries its own implementation guides under `references/`. **Do not duplicate skill content into specs/design/tasks** — task entries should reference the skill, not inline its instructions.

---

## Restrictions

- Never modify a migration that has already been applied — create a new one.
- Never make direct DB calls from Application — use repository interfaces.
- Never expose Domain entities directly in API responses — use DTOs.
- Never put business logic in endpoints — delegate to use cases.
- Never use `dynamic` or `object` instead of typed DTOs.
- Never create a repository without its interface in Domain.
- Never mix responsibilities across layers (e.g. NHibernate types leaking into Application).
- Never hardcode user-facing strings — use resource files for messages once introduced.
- Always return `Result<T>` from FluentResults in Application; never throw exceptions for business errors.
- All entities inherit `AbstractDomainObject`; keep properties `virtual` for NHibernate proxying.
- Transactions: open and commit/rollback through `IUnitOfWork` — never bypass it.
- All async methods use the `Async` suffix.
- Use FluentValidation for every request/entity validation.
- Use AutoMapper for entity-to-DTO mapping in WebApi.
- Domain has no external runtime dependencies beyond FluentValidation.

---

## Testing setup

All four `tests/*` projects share the same harness configured in `Directory.Packages.props`:

- **NUnit 4** as the test framework (auto-imported via `<Using Include="NUnit.Framework" />` in each test `.csproj`).
- **FluentAssertions 8** for assertions, **AutoFixture.AutoMoq** for fixtures and mocks, **Castle.Core** for proxying.
- **`Microsoft.AspNetCore.Mvc.Testing`** is available for endpoint integration tests via `WebApplicationFactory`.
- **Coverlet** collector is wired for coverage.

When adding tests, follow the per-layer test patterns documented in `dca-create-tests` (DomainTestBase, ApplicationTestBase, NDbUnit scenarios for repositories, `WebApplicationFactory` for endpoints).

---

## Git workflow

Never commit or push unless explicitly asked. Wait for confirmation before any git operation.

- **MCP server**: `github-personal` — use this server for all GitHub operations (PRs, issues, reviews) against `omsanchezo/reservation-hands-on-lab.backend`.
- **Base branch / main**: `master`
- Feature branches branch off `master` and merge back through PRs.
- Use Conventional Commit prefixes (`feat`, `fix`, `chore`, `docs`, `test`, `refactor`) — recent history follows this style.

---

## Code review workflow

When the user asks for a **code review** (e.g. "code review", "review PR", "review code", "revisa el código"), **always invoke the `dca-review-pr-code` skill first** using the Skill tool. Do NOT attempt the review manually or launch agents before loading the skill — the skill carries the structured process, checklists, and standards required for APSYS reviews.
