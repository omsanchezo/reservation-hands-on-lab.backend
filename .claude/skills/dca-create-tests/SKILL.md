---
name: create-tests
description: >-
  Guides creation of unit, integration, and endpoint tests for APSYS .NET backend
  projects following Clean Architecture. Covers domain entity tests (AutoFixture,
  DomainTestBase), application handler tests (Moq, ApplicationTestBase), NHibernate
  repository integration tests (NDbUnit scenarios), and FastEndpoints endpoint tests
  (WebApplicationFactory). Use when user asks to "create tests", "write unit tests",
  "add integration tests", "test an entity", "test a handler", "test a repository",
  "test an endpoint", "add test coverage", or "write tests for this class" in Clean
  Architecture.
compatibility: >-
  Requires .NET backend projects using Clean Architecture with NUnit, FluentAssertions,
  AutoFixture, Moq, NHibernate, NDbUnit, and FastEndpoints.
metadata:
  author: APSYS
  version: 1.0.0
---

# Backend Testing Skill

Guide for creating tests across all Clean Architecture layers. Each layer has its own test base class, naming conventions, and minimum coverage requirements, but all share common patterns: AAA structure, FluentAssertions with "because" messages, and AutoFixture for test data.

## Instructions

### Step 1: Identify the Test Layer

Determine which layer the target code belongs to. If the user says "test this class" without specifying, **infer from the namespace or project name**:

| Namespace / Project contains | Layer | Test Type | Reference |
|------------------------------|-------|-----------|-----------|
| `*.Domain` | Domain | Unit tests | `references/domain-testing.md` |
| `*.Application` | Application | Unit tests (mocked) | `references/application-testing.md` |
| `*.Infrastructure` | Infrastructure | Integration tests (DB) | `references/integration-testing.md` |
| `*.WebApi` | Endpoint | Integration tests (HTTP) | `references/endpoint-testing.md` |

If the target class doesn't clearly belong to one layer (e.g., a shared utility), ask the developer which layer's test patterns to follow.

### Step 2: Explore Existing Test Patterns

Before writing tests:

1. **Find the test project** for the target layer (e.g., `tests/{Project}.Domain.Tests/`)
2. **Read existing test classes** to match:
   - Test base class usage (`DomainTestBase`, `ApplicationTestBase`, `NHRepositoryTestBase`, `EndpointTestBase`)
   - Fixture configuration and customizations
   - Scenario loading patterns (for integration/endpoint tests)
   - Folder structure and file naming

### Step 3: Follow the Layer-Specific Pattern

Read the corresponding reference file for full guidance. Key differences per layer:

#### Domain Tests
- **Base class:** `DomainTestBase` with `IFixture`
- **Focus:** Entity construction, property validation, business rules
- **Minimum per entity:** Constructor defaults, constructor with params, IsValid happy path, IsValid negative per rule, GetValidator type, Validate error details
- **No mocks needed** — pure domain logic

#### Application Tests
- **Base class:** `ApplicationTestBase` with `AutoMoqCustomization`
- **Focus:** Handler behavior, Result pattern, transaction management
- **Minimum per handler:** Varies by CRUD type (Create=5, Get=3, GetManyAndCount=3, Update=5, Delete=4)
- **Mock:** `IUnitOfWork`, repositories, `ILogger` via Moq

#### Integration Tests (Repository)
- **Base class:** `NHRepositoryTestBase<TRepo, T, TKey>`
- **Focus:** NHibernate mapping correctness, CRUD against real DB
- **Cardinal rule:** Never use the repository under test in Arrange or Assert — use `LoadScenario()` for Arrange, `nDbUnitTest.GetDataSetFromDb()` for Assert
- **Requires:** NDbUnit scenarios (XML seed data)

#### Endpoint Tests
- **Base class:** `EndpointTestBase` with `CustomWebApplicationFactory`
- **Focus:** Full HTTP request/response cycle, status codes, auth
- **Minimum per endpoint:** Varies by type (Create=4, Get=3, GetManyAndCount=3, Update=5, Delete=3) — always include 401 unauthorized
- **Also test:** AutoMapper mapping profiles via `BaseMappingProfileTests`

### Step 4: Handle Cross-Layer Testing

When a test scenario touches multiple layers:

- **Repository integration tests** validate both NHibernate mapping AND domain entity persistence. Use `NHRepositoryTestBase` — it covers the domain-infrastructure boundary.
- **Endpoint tests** exercise the full stack (HTTP → endpoint → handler → repository → DB). Use `EndpointTestBase` — it covers all layers but focus assertions on HTTP behavior.
- **If testing a handler that calls multiple repositories**, mock each repository independently in `ApplicationTestBase`. Test the orchestration logic, not the repository behavior.
- **Rule of thumb:** Test at the highest layer that covers your scenario, but keep assertions focused on that layer's responsibility.

### Step 5: Validate with Checklist

Before submitting tests, verify:

- [ ] Test class name follows `{EntityName}Tests` (domain) or `{Action}{Entity}EndpointTests` (endpoint) convention
- [ ] Test methods follow `{Method}_{Scenario}_{ExpectedResult}` naming
- [ ] AAA pattern with explicit `// Arrange`, `// Act`, `// Assert` comments
- [ ] FluentAssertions used with `.Should()` and mandatory `because` messages
- [ ] AutoFixture used for test data generation (no manual object construction)
- [ ] Minimum test count met for the layer and operation type
- [ ] No anti-patterns: tests don't use the component under test in Arrange/Assert (integration/endpoint)
- [ ] Integration/endpoint tests use scenarios for seed data, not manual inserts

## References

| Reference | Layer | Content |
|-----------|-------|---------|
| `references/domain-testing.md` | Domain | DomainTestBase, entity validation tests, property patterns, AutoFixture |
| `references/application-testing.md` | Application | ApplicationTestBase, handler mocking, Result pattern, transaction tests |
| `references/integration-testing.md` | Infrastructure | NHRepositoryTestBase, NDbUnit scenarios, DB verification, scenario builder |
| `references/endpoint-testing.md` | WebApi | EndpointTestBase, WebApplicationFactory, HTTP tests, mapping profile tests |

## Examples

### Example 1: "Test this entity"
- **Trigger:** User points to `src/{Project}.Domain/Entities/Prototype.cs`
- **Action:** Infer Domain layer from namespace → read `references/domain-testing.md` → create `PrototypeTests.cs` with constructor, validation, and business rule tests using `DomainTestBase`

### Example 2: "Add tests for the CreateUser handler"
- **Trigger:** User references a handler in `*.Application`
- **Action:** Infer Application layer → read `references/application-testing.md` → create `CreateUserHandlerTests.cs` with 5 minimum tests (happy path, validation error, duplicate, commit verification, rollback on exception) using `ApplicationTestBase`

### Example 3: "Write integration tests for the UserRepository"
- **Trigger:** User explicitly asks for repository/integration tests
- **Action:** Read `references/integration-testing.md` → create `UserRepositoryTests.cs` using `NHRepositoryTestBase<UserRepository, User, Guid>` → check if NDbUnit scenarios exist, create them if not

### Example 4: "Test the GET /api/prototypes endpoint"
- **Trigger:** User references an endpoint in `*.WebApi`
- **Action:** Infer Endpoint layer → read `references/endpoint-testing.md` → create `GetPrototypeEndpointTests.cs` with 3 minimum tests (200 found, 404 not found, 401 unauthorized) using `EndpointTestBase`

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| AutoFixture creates circular references | Missing `OmitOnRecursionBehavior` | Verify test base class sets `fixture.Behaviors.Add(new OmitOnRecursionBehavior())` |
| Integration test can't find scenario XML | Scenario not generated or wrong path | Run scenario builder: `dotnet run` in scenarios project. Check `.env` for `SCENARIOS_FOLDER_PATH` |
| Endpoint test returns 401 unexpectedly | `TestAuthHandler` not registered | Verify `CustomWebApplicationFactory` adds `TestAuthHandler` in `ConfigureTestServices` |
| Moq setup not matching calls | Argument mismatch in `Setup()` | Use `It.IsAny<T>()` for flexible matching, verify argument types match exactly |
| FluentAssertions `because` missing | Coding standard requires it | Every `.Should()` call must end with `, because: "reason"` |
| NDbUnit `GetDataSetFromDb()` returns empty | Wrong table name or schema | Verify table name matches DB schema (lowercase in PostgreSQL) |
