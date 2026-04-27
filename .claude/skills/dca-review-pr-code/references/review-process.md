# Review Process Reference

Guide for running PR reviews, severity definitions, report template, and common review scenarios for APSYS .NET backend projects.

---

## 1. PR Diff Workflow

### Step 1: Resolve the PR Base Branch

Determine which branch the current branch would merge into (the PR target) using three-level priority:

1. **User says it explicitly** -- if the user specifies the base branch (e.g., "Review this PR against develop"), use that value directly. Always takes precedence.
2. **`BASE_BRANCH` variable in CLAUDE.md** -- read the project's `CLAUDE.md` and look for `BASE_BRANCH=<branch-name>`. If found, use that value.
3. **Auto-detect via merge-base heuristic** -- compare merge-base distances to find the **closest ancestor**, but **always confirm with the user** before proceeding.

```bash
# 1. Get current branch name
current=$(git rev-parse --abbrev-ref HEAD)

# 2. Guard: check if already on a base branch
for base in main master develop devel; do
  [ "$current" = "$base" ] && echo "ON_BASE:$base" && break
done

# 3. Find the closest base branch by merge-base distance
best_branch=""
best_count=999999
for branch in main master develop devel; do
  git rev-parse --verify "$branch" &>/dev/null || continue
  base=$(git merge-base "$branch" HEAD 2>/dev/null) || continue
  count=$(git rev-list --count "$base"..HEAD)
  echo "$branch: $count commits ahead"
  if [ "$count" -lt "$best_count" ]; then
    best_count=$count
    best_branch=$branch
  fi
done
echo "BEST:$best_branch"
```

**How it works:** For each candidate branch, `git merge-base` finds the common ancestor with HEAD, then `git rev-list --count` measures how many commits HEAD is ahead. The branch with the fewest commits ahead is the most likely fork point.

When using auto-detection (level 3), ask the user to confirm: "Detected `<best_branch>` as the base branch (<N> commits ahead). Is this correct?" Only proceed after confirmation.

**Guards before proceeding:**

- **On main/master/develop/devel directly:** No PR context exists. Inform the user: "You are on a base branch. Switch to a feature branch to review a PR, or specify which commits or files to review."
- **No base branch found locally:** Suggest `git fetch origin` first, or ask the user which branch the PR would target.
- **Detached HEAD:** Not a branch-based workflow. Ask the user to check out a feature branch.
- **Multiple candidates tie:** Prefer the more specific integration branch (`develop`/`devel` over `main`/`master`).

### Step 2: List Changed Files in the PR

```bash
# List all files that would be included in the pull request
git diff --name-status <base-branch>...HEAD

# Example output:
# A  src/project.domain/entities/Product.cs
# A  src/project.domain/entities/validators/ProductValidator.cs
# A  src/project.application/usecases/products/CreateProductUseCase.cs
# A  src/project.infrastructure/mappers/ProductMapper.cs
# A  src/project.infrastructure/repositories/ProductRepository.cs
# M  src/project.infrastructure/NHUnitOfWork.cs
# A  src/project.webapi/endpoints/products/CreateProductEndpoint.cs
# A  src/project.webapi/models/products/CreateProductModel.cs
```

**Important:** The `...` (triple-dot) syntax shows changes since the branch diverged from base, which is exactly what a PR contains. Do NOT use `..` (double-dot).

**PR size check:** If the diff contains more than 30 changed files, note this in the report and suggest splitting into smaller PRs.

### Step 2b: Validate Project Health

Before reviewing code, verify the project compiles and tests pass:

```bash
dotnet build
dotnet test   # if test project exists
```

- If **build fails**, report compilation errors and stop the review.
- If **tests fail**, report failing tests and note whether they are pre-existing or introduced by the branch.
- Register any warnings for inclusion in the report.

**Note:** This step requires running in the project directory. If invoked without shell access, skip this step and proceed with static analysis only.

### Step 3: Categorize Files

Group changed files by layer for systematic review:

| Layer | File Patterns | Review Focus |
|-------|--------------|--------------|
| Domain Entities | `*.domain/entities/**/*.cs` | AbstractDomainObject, virtual, constructors, GetValidator |
| Domain Validators | `*.domain/entities/validators/**/*.cs` | FluentValidation, WithMessage, WithErrorCode |
| Domain DAOs | `*.domain/daos/**/*.cs` | POCO, virtual, SearchAll, no inheritance |
| Domain Interfaces | `*.domain/interfaces/**/*.cs` | IRepository, IReadOnlyRepository, IUnitOfWork |
| Domain Exceptions | `*.domain/exceptions/**/*.cs` | Exception type hierarchy |
| Domain Resources | `*.domain/resources/**/*.cs` | Constants, allowed values |
| Application | `*.application/usecases/**/*.cs` | Command/Handler, FluentResults, transactions |
| Infrastructure Mappers | `*.infrastructure/mappers/**/*.cs` | ClassMapping, Schema, Table, Relations |
| Infrastructure Repos | `*.infrastructure/repositories/**/*.cs` | NHRepository, validation, duplicate checks |
| Infrastructure UoW | `*.infrastructure/*UnitOfWork*.cs` | Lazy creation, Dispose, transactions |
| Infrastructure DI | `*.infrastructure/*SessionFactory*.cs` | DI registration, session lifecycle |
| WebApi Endpoints | `*.webapi/endpoints/**/*.cs` | BaseEndpoint, HTTP verbs, error mapping |
| WebApi Models | `*.webapi/models/**/*.cs` | Request/Response, DTOs |
| WebApi Profiles | `*.webapi/profiles/**/*.cs` | AutoMapper, mapping directions |
| Migrations | `*.migrations/**/*.cs` | Up/Down, reversibility, naming |
| Tests | `*.tests*/**/*.cs` | Coverage, patterns, AAA |

### Step 4: Read and Review Each File

Read files in this order for best context:
1. Domain entities and validators (understand the domain model)
2. Repository interfaces (understand the contract)
3. DAOs (understand read-only projections)
4. Use cases/handlers (understand business operations)
5. Infrastructure mappers (understand ORM mapping)
6. Repository implementations (understand persistence)
7. Unit of Work (verify registration)
8. DTOs and request/response models (understand API contract)
9. AutoMapper profiles (verify mapping configuration)
10. Endpoints (understand HTTP layer)
11. Migrations (understand schema changes)
12. Tests (verify coverage)
13. Config files (DI, appsettings)

### Step 5: Generate Report

Compile findings into the report template (section 4).

### Step 6: Save Report

Save the generated report as a markdown file in the project's `.claude/reviews/` directory:

```
.claude/reviews/{branch-name}-review.md
```

- Create the `.claude/reviews/` directory if it does not exist.
- The file name uses the branch name (e.g., `kc-350-users-crud-backend-review.md`).
- If a report already exists for the same branch, overwrite it with the new version.

---

## 2. Severity Definitions

### CRITICO (Bloquea aprobacion)

Issues that break architectural patterns, will cause bugs, or violate core rules. **Blocks approval -- must fix before merge.**

Examples:
- Entity missing `virtual` on properties (NHibernate proxy failure)
- Entity not overriding `GetValidator()` (validation never runs)
- Handler missing `Rollback()` in catch block after `BeginTransaction()`
- Using `Add()` on an existing entity instead of `Save()` (StaleObjectStateException)
- Domain layer importing Infrastructure or WebApi (layer violation)
- Handler accessing `ISession` directly instead of through repository
- Missing repository registration in `IUnitOfWork`
- Endpoint returning wrong HTTP status code for exception type
- GetManyAndCount handler returning `Result.Fail()` instead of rethrowing
- Missing `Mutable(false)` on DAO mapper that maps to a VIEW
- Business logic in handler instead of domain entity/validator
- SQL injection vulnerability (string concatenation in queries)
- DAO mapper not marked `Mutable(false)` when mapped to a VIEW

### IMPORTANTE (Debe corregirse)

Code smells, convention deviations, or maintainability concerns. **Must be corrected before merge.**

Examples:
- Validator rule missing `WithMessage()` or `WithErrorCode()`
- Wrong exception catch order (generic before specific)
- Entity property not initialized with default value
- Namespace does not match folder structure
- Missing XML documentation on public API
- Handler not using primary constructor injection
- AutoMapper profile missing `ForMember` for non-trivial mappings
- Migration without proper `Down()` implementation
- Test class not following naming conventions
- Missing `BeginTransaction()` for write operations
- Endpoint not configuring Swagger error responses
- DAO missing `SearchAll` property

### MENOR (Sugerencia de mejora)

Improvements that would make the code better but are not incorrect. **Consider for future improvement.**

Examples:
- Missing XML documentation on internal methods
- Resource class could be added for magic string constants
- Test could cover additional edge cases
- Validator could use more specific error codes
- Method could be simplified with pattern matching
- Migration description could be more descriptive
- Variable naming could be more descriptive

### Report Status Logic

The report **Estado** is determined by the highest severity found:

| Condition | Estado |
|-----------|--------|
| No issues or only trivial MENORes | **Aprobado** |
| Has IMPORTANTEs and/or MENORes, no CRITICOs | **Aprobado con observaciones** |
| Has at least one CRITICO | **Necesita correcciones** |

---

## 3. Review Checklist (Quick Reference)

Use this checklist during review to ensure all areas are covered:

### Domain Layer

```
[ ] Entity inherits AbstractDomainObject
[ ] All entity properties are virtual
[ ] Two constructors: empty (NHibernate) + parameterized (creation)
[ ] GetValidator() overridden returning correct validator
[ ] Properties initialized with defaults (string.Empty, new List<>())
[ ] No ORM attributes or persistence logic
[ ] Validator inherits AbstractValidator<T>
[ ] Every rule has WithMessage() + WithErrorCode()
[ ] Resources class for constants (allowed values, magic strings)
[ ] DAO is POCO without AbstractDomainObject
[ ] DAO has SearchAll property
[ ] DAO has virtual properties
[ ] Repository interface in domain/interfaces/
[ ] Repository registered in IUnitOfWork
```

### Application Layer

```
[ ] Outer class with nested Command and Handler
[ ] Command implements ICommand<Result<T>> (or ICommand<Result> for void)
[ ] Handler implements ICommandHandler<TCommand, TResult>
[ ] Primary constructor injection for dependencies
[ ] BeginTransaction before write operations
[ ] Commit on success, Rollback in catch
[ ] Catch block order: specific exceptions first, generic last
[ ] Result.Ok() on success, Result.Fail() on failure
[ ] GetManyAndCount: Rollback + throw (not Result.Fail)
[ ] No business logic in handler (delegate to domain)
```

### Infrastructure Layer

```
[ ] ClassMapping<T> with Schema, Table, Id, Properties
[ ] Entity mapper: correct NHibernateUtil types
[ ] Entity mapper: relationships (ManyToOne, OneToMany, Bag)
[ ] DAO mapper: Mutable(false) mandatory
[ ] DAO mapper: mapped to VIEW
[ ] DAO mapper: SearchAll with CONCAT_WS formula
[ ] Repository implements IRepository or IReadOnlyRepository
[ ] CreateAsync: validate, check duplicates, persist
[ ] UpdateAsync: get, check duplicates excluding self, update, validate, persist
[ ] Repository registered in IUnitOfWork with lazy creation
[ ] Dispose pattern: transaction first, then session
```

### WebApi Layer

```
[ ] Endpoint inherits BaseEndpoint<TRequest, TResponse>
[ ] Correct HTTP verb (POST/GET/PUT/DELETE)
[ ] Correct HTTP status (201/200/200/204)
[ ] ExceptionalError switch for domain exceptions
[ ] Request/Response as nested classes in Model
[ ] AutoMapper profile for all mapping directions
[ ] DontThrowIfValidationFails() for create/update
[ ] Swagger error documentation
```

### Migrations

```
[ ] Sequential version number
[ ] Descriptive migration name
[ ] Up() creates/alters schema
[ ] Down() reverses changes
[ ] No destructive operations without documentation
[ ] Idempotent seeds (ON CONFLICT DO NOTHING)
[ ] No hardcoded secrets in seed data
```

### Security

```
[ ] No hardcoded credentials or connection strings
[ ] Parameterized queries (no string concatenation in SQL)
[ ] Input validation via FluentValidation
[ ] No sensitive data in log messages
[ ] Authentication/authorization on endpoints
[ ] No secrets in migration seed data
```

---

## 4. Report Template

The report is written **entirely in Spanish**. All section headings, field labels, descriptions, and conclusions use Spanish.

```markdown
# Peer Review: {branch-name}

> **Generado con:** review-pr-code v1.0.0
> **Fecha:** YYYY-MM-DD

---

**Revisor:** Claude Code
**Estado:** Aprobado | Aprobado con observaciones | Necesita correcciones

## Resumen Ejecutivo

[1-3 sentences: overall quality, what the branch does, key findings summary. Keep it concise but informative enough for someone to understand the PR scope without reading further.]

## Informacion del Branch

- **Branch:** {branch-name}
- **Base:** {detected-base-branch}
- **Commits:** N
- **Archivos modificados:** N
- **Lineas:** +N / -N

## Archivos Revisados

| Archivo | Capa | Issues |
|---------|------|--------|
| `entities/User.cs` | Domain | 0 |
| `entities/validators/UserValidator.cs` | Domain | 1 |
| `usecases/users/CreateUserUseCase.cs` | Application | 0 |
| `mappers/UserMapper.cs` | Infrastructure | 0 |
| `endpoints/users/CreateUserEndpoint.cs` | WebAPI | 0 |

*List every changed file. Use layer names: Domain, Infrastructure, Application, WebAPI, Migrations, Tests.*

## Issues Encontrados

### Criticos

Ninguno.

_(Or list issues using the structure below)_

### Importantes

#### Issue #1: [Short descriptive title of the problem]

- **Archivo:** `entities/validators/UserValidator.cs:15-18`
- **Tipo:** Domain / Validation
- **Descripcion:** Validator rule for Email is missing `WithErrorCode()`. Without an error code, the client cannot programmatically identify which validation failed, making error handling in the frontend unreliable.
- **Guia:** `skills/create-domain/references/validators.md`
- **Sugerencia:**

\```csharp
// Codigo actual
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("The [Email] cannot be null or empty");

// Codigo sugerido
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("The [Email] cannot be null or empty")
    .WithErrorCode("Email");
\```

### Menores

#### Issue #2: [Short descriptive title]

- **Archivo:** `usecases/users/CreateUserUseCase.cs:42`
- **Tipo:** Code Quality / Naming
- **Descripcion:** [What could be improved and why]
- **Sugerencia:** [What to do, optionally with code block]

## Checklist de Cumplimiento

| Capa | Status | Observaciones |
|------|--------|---------------|
| Domain | OK | Entidades y validadores correctos |
| Infrastructure | OK/Observaciones | [brief note about findings] |
| Application | OK | Orquestacion correcta, Result pattern |
| WebAPI | OK/Observaciones | [brief note] |
| Migrations | OK | Reversible, versionado correcto |
| Tests | OK/Observaciones | [brief note] |

> Use N/A when no files were modified in a layer. Add a note below the table when an observation refers to a pre-existing issue not introduced by this branch.

## Aspectos Positivos

- **[Bold title describing what was done well]**: [Detailed explanation of why it matters, referencing specific files and lines when relevant.]
- **[Another positive aspect]**: [Explanation]

*Always include positive aspects. Highlight good architectural decisions, proper pattern usage, comprehensive validation, good error handling, and thorough testing.*

## Conclusion

[1-2 paragraph assessment: summarize the overall quality, whether issues are blocking or not, and the general state of the code. End with concrete next steps if any actions are required, e.g., "Proximos pasos: (1) corregir X, (2) agregar Y." If no actions: "El branch esta listo para merge."]
```

### Issue Structure Reference

Every issue (CRITICO, IMPORTANTE, or MENOR) follows this structure:

| Field | Required | Description |
|-------|----------|-------------|
| **Archivo** | Yes | File path with line numbers. Can list multiple files if the issue spans several. |
| **Tipo** | Yes | Category: Architecture, Domain, Application, Infrastructure, WebApi, Migration, Security, Data Access, Code Quality, Naming, Error Handling, Testing, etc. |
| **Descripcion** | Yes | What is wrong, why it matters, and the impact. Note if related to a pre-existing pattern. |
| **Guia** | No | Path to the project guide or skill reference that defines the violated rule. Only include when a specific guide applies. |
| **Sugerencia** | Yes | How to fix it. For CRITICO and IMPORTANTE, include a code block with "Codigo actual" vs "Codigo sugerido". For MENOR, a text suggestion may suffice. |

---

## 5. Common Review Scenarios

### Scenario 1: New CRUD Feature (Full Stack)

When reviewing a new CRUD feature across all layers, check in this order:

1. **Domain entity**: AbstractDomainObject, virtual properties, constructors, GetValidator
2. **Validator**: FluentValidation rules, WithMessage, WithErrorCode
3. **Repository interface**: IRepository<T, Guid>, custom methods, IUnitOfWork registration
4. **DAO**: POCO, virtual, SearchAll, no inheritance
5. **Use cases**: All 5 handlers (Create, Get, GetManyAndCount, Update, Delete)
6. **Mappers**: Entity mapper + DAO mapper (Mutable false, VIEW)
7. **Repository implementation**: NHRepository, validation, duplicate checks
8. **Unit of Work**: New repository registered
9. **DTOs and models**: Request/Response for each endpoint
10. **AutoMapper profiles**: All mapping directions
11. **Endpoints**: Correct HTTP verbs, status codes, error handling
12. **Migrations**: Table creation, indexes, foreign keys
13. **Tests**: Unit tests for domain, application tests for handlers, integration tests for endpoints

Expected files for a full CRUD feature:
```
# Domain
entities/Product.cs
entities/validators/ProductValidator.cs
daos/ProductDao.cs
interfaces/IProductRepository.cs
resources/ProductResource.cs  (optional)

# Application
usecases/products/CreateProductUseCase.cs
usecases/products/GetProductUseCase.cs
usecases/products/GetManyAndCountProductsUseCase.cs
usecases/products/UpdateProductUseCase.cs
usecases/products/DeleteProductUseCase.cs

# Infrastructure
mappers/ProductMapper.cs
mappers/ProductDaoMapper.cs
repositories/ProductRepository.cs
NHUnitOfWork.cs  (modified - add repository property)

# WebApi
endpoints/products/CreateProductEndpoint.cs
endpoints/products/GetProductEndpoint.cs
endpoints/products/GetManyAndCountProductsEndpoint.cs
endpoints/products/UpdateProductEndpoint.cs
endpoints/products/DeleteProductEndpoint.cs
models/products/CreateProductModel.cs
models/products/GetProductModel.cs
models/products/GetManyAndCountProductsModel.cs
models/products/UpdateProductModel.cs
models/products/DeleteProductModel.cs
dtos/ProductDto.cs
profiles/ProductMappingProfile.cs

# Migrations
M###CreateProductTable.cs

# Tests
domain/ProductTests.cs
application/CreateProductHandlerTests.cs
# ... etc.
```

### Scenario 2: Adding a New Use Case to Existing Feature

When a new use case is added:

1. **Command**: Correct properties, implements ICommand<Result<T>>
2. **Handler**: Primary constructor injection, transaction management, error handling
3. **Endpoint**: New endpoint with correct route, verb, and status code
4. **Model**: Request/Response classes
5. **AutoMapper profile**: New mappings added
6. **Tests**: Handler tests and endpoint tests

### Scenario 3: Modifying Domain Model

When entities or validators are modified:

1. **Entity changes**: Property additions maintain virtual, defaults, constructors
2. **Validator changes**: New rules have WithMessage + WithErrorCode
3. **Mapper updates**: New properties mapped correctly
4. **Migration**: Schema change reflected in migration
5. **DAO update**: If property should be in listing, DAO and DAO mapper updated
6. **No breaking changes**: Existing handlers/endpoints still work

### Scenario 4: Bug Fix in Existing Handler

When fixing a bug in an existing handler:

1. **Root cause**: The fix addresses the actual problem
2. **Side effects**: No new anti-patterns introduced
3. **Error handling**: Fix follows error handling conventions
4. **Transaction safety**: Rollback paths still correct
5. **Tests**: Regression test added for the bug

### Scenario 5: Migration Changes

When reviewing migration files:

1. **Sequential version**: Number is in correct sequence
2. **Naming**: Descriptive class name (e.g., `M005AddStatusColumnToProducts`)
3. **Up/Down**: Both methods implemented
4. **Reversibility**: Down properly reverses Up
5. **Data safety**: No data loss without explicit documentation
6. **Seeds**: Idempotent with ON CONFLICT DO NOTHING
7. **No secrets**: No credentials or sensitive data in seeds

---

## 6. Review Tips

### Reading Entities

When reviewing an entity, verify:
1. Inherits `AbstractDomainObject`
2. ALL properties are `virtual` (including navigation properties)
3. Empty constructor exists (NHibernate)
4. Parameterized constructor includes essential creation properties (NOT Id, NOT CreationDate)
5. `GetValidator()` returns correct validator type
6. Collections initialized at property level (`= new List<>()`)
7. No ORM attributes, no persistence logic, no serialization attributes

### Reading Validators

When reviewing a validator, verify:
1. Inherits `AbstractValidator<EntityType>`
2. Every rule chains `WithMessage()` + `WithErrorCode()`
3. Error codes follow naming: `"PropertyName"` or `"PropertyName_SpecificError"`
4. Messages use format: `"The [{Property}] ..."` in English
5. Constants come from Resource classes (no inline magic strings)
6. Custom validations use `Must()` with clear predicate

### Reading Use Case Handlers

When reviewing a handler, verify:
1. Outer class with nested `Command` + `Handler`
2. Command implements `ICommand<Result<T>>` (or `ICommand<Result>` for delete)
3. Handler uses primary constructor injection
4. Write operations: `BeginTransaction` -> operation -> `Commit`
5. Catch blocks in correct order: specific exceptions first
6. `InvalidDomainException` -> `Result.Fail()` with deserialized validation errors
7. `DuplicatedDomainException` -> `Result.Fail()` with duplicate message
8. `ResourceNotFoundException` -> `Result.Fail()` with not found message
9. Generic `Exception` -> `Result.Fail()` with logger and CausedBy
10. GetManyAndCount: `Rollback()` + `throw;` (no Result.Fail)

### Reading Mappers

When reviewing a mapper, verify:
1. `ClassMapping<EntityType>` with correct generic
2. Configuration order: Schema -> Table -> Id -> Properties -> Relations
3. `NHibernateUtil` types match C# types
4. Restrictions: `NotNullable()`, `Unique()`, `Length(n)` as needed
5. Relations: `ManyToOne`, `OneToMany`, `Bag`/`ManyToMany` with correct cascade
6. DAO mappers: `Mutable(false)` is mandatory

### Reading Endpoints

When reviewing an endpoint, verify:
1. Inherits `BaseEndpoint<TRequest, TResponse>` (or `Endpoint<TRequest>` for delete)
2. Route and HTTP verb match the operation
3. `DontThrowIfValidationFails()` for create/update
4. Result handling: `result.IsSuccess` branch first
5. Error handling: `ExceptionalError` switch matches domain exceptions
6. Correct HTTP status: 201 Created, 200 OK, 204 No Content
7. AutoMapper used for request->command and entity->response conversions

### Reading Repositories

When reviewing a repository, verify:
1. Inherits `NHRepository<T, Guid>` or `NHReadOnlyRepository<T, Guid>`
2. `CreateAsync`: validate entity, check for duplicates, then persist
3. `UpdateAsync`: get existing, check duplicates (excluding self), update props, validate, persist
4. Custom queries use HQL or NHibernate API (no raw SQL unless justified)
5. `GetByXXXAsync` methods have null guards with `ResourceNotFoundException`
6. Registered in `IUnitOfWork` with lazy pattern

---

## 7. When NOT to Flag

Not everything is a violation. These are acceptable:

- Entity without DAO (not all entities need listing views)
- Feature without migrations (if using existing tables)
- Handler without all CRUD operations (partial features are valid)
- Using `var` when the type is obvious from the right-hand side (`var user = new User()`)
- Empty `Down()` on irreversible migrations if explicitly documented why
- Missing XML docs on private/internal methods
- Test class with fewer tests than the "minimum" if the feature is simple
- DAO without all entity properties (DAOs are projections, not mirrors)
- Repository with no custom methods beyond base class
- Endpoint without Swagger annotations on simple CRUD
- Migration using raw SQL when FluentMigrator API cannot express the operation
- Formatting/style issues handled by dotnet format or IDE settings (spacing, braces, etc.)

---

## 8. Review Principles

1. **Objectivity**: Base feedback on standards and guides, not personal opinions
2. **Constructiveness**: Always offer suggestions for improvement, not just point out errors
3. **Completeness**: Review all changed files in the PR without exception
4. **Prioritization**: Classify issues by severity to facilitate correction
5. **Traceability**: Reference specific rules and skill guides for each finding
6. **Pragmatism**: Focus on real problems, avoid excessive perfectionism
