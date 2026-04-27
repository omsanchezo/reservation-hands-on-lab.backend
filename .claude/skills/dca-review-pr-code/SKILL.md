---
name: review-pr-code
description: >-
  Generates structured PR review reports with severity-graded findings,
  fix recommendations, and standards compliance checklists for .NET
  backend code following APSYS Clean Architecture standards. Reviews
  only the changes between the current branch and its base branch.
  Produces markdown reports covering architecture layers (Domain,
  Application, Infrastructure, WebApi), C# conventions, data access
  patterns, and anti-patterns. Use when user asks to "review PR",
  "review pull request", "review code", "code review", "check quality",
  "review backend", or "validate standards" in .NET Clean Architecture
  projects.
compatibility: >-
  Requires .NET 9.0 backend projects using Clean Architecture with
  NHibernate, FluentValidation, FastEndpoints, FluentResults,
  AutoMapper, and FluentMigrator. Works with Claude Code and Claude.ai.
metadata:
  author: APSYS
  version: 1.0.0
---

# Review PR Code Skill

Generates structured PR review reports with severity-graded findings, fix recommendations, and standards compliance checklists for APSYS .NET backend projects. Reviews only the diff between the current branch and its base branch.

## Instructions

### Step 1: Identify PR Scope

Determine the base branch and find files that would be included in a pull request.

**2a. Resolve base branch** using a three-level priority:

1. **User says it explicitly** -- if the user specifies the base branch in their prompt (e.g., "Review this PR against develop"), use that value directly. This always takes precedence.
2. **`BASE_BRANCH` variable in CLAUDE.md** -- read the project's `CLAUDE.md` and look for `BASE_BRANCH=<branch-name>`. If found, use that value.
3. **Auto-detect via merge-base heuristic** -- find the closest ancestor among candidate branches, but **always confirm with the user** before proceeding:

```bash
# Verify current branch
current=$(git rev-parse --abbrev-ref HEAD)

# Guard: if on a base branch directly, there is no PR to review
for base in main master develop devel; do
  [ "$current" = "$base" ] && echo "ON_BASE:$base" && break
done

# Find the candidate with the fewest commits ahead (closest merge-base)
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

When using auto-detection (level 3), **ask the user to confirm**: "Detected `<best_branch>` as the base branch (<N> commits ahead). Is this correct?" Only proceed after confirmation.

**Guards:**
- If on main/master/develop/devel directly -> there is no PR to review. Inform the user and ask which commits or files to review instead.
- If no candidate branch is found locally -> ask the user which branch the PR would target.
- If multiple candidates tie at the same commit count -> prefer the more specific branch (`develop`/`devel` over `main`/`master`).
- If the diff is empty -> inform that no changes were found between the base and HEAD.

**1b. List changed files** between the base branch and HEAD:

```bash
git diff --name-status <base-branch>...HEAD
```

**1c. Classify** files into categories:
- **Domain** (entities, validators, value objects, exceptions, DAOs, repository interfaces, resources)
- **Application** (use cases, commands, handlers)
- **Infrastructure** (mappers, repositories, unit of work, session factory, DI)
- **WebApi** (endpoints, request/response models, DTOs, AutoMapper profiles)
- **Migrations** (FluentMigrator migration files)
- **Tests** (unit tests, integration tests)
- **Config** (Program.cs, appsettings, project files)

Read files in this order for best context: Domain entities/validators -> Repository interfaces -> DAOs -> Use cases/handlers -> Mappers -> Repositories -> Unit of Work -> DTOs/Models -> AutoMapper profiles -> Endpoints -> Migrations -> Tests -> Config.

**1d. Validate project health** (when running in a project directory):

Run `dotnet build` and `dotnet test` (if test project exists). If the build fails, report errors and stop the review. If the PR changes more than 30 files, suggest splitting into smaller PRs.

> Skip this step when shell access is unavailable (e.g., Claude.ai without a project). Proceed with static analysis only.

> For the complete base branch detection algorithm, project health checks, and edge cases, see `references/review-process.md`

### Step 2: Review Architecture Compliance

Validate each changed file against Clean Architecture rules:
- **Layer boundaries**: Domain has no dependencies on Application/Infrastructure/WebApi. Application depends only on Domain. Infrastructure implements Domain interfaces. WebApi depends on Application.
- **Project structure**: Files in correct namespace (`{project}.domain.entities`, `{project}.application.usecases.{feature}`, `{project}.infrastructure.repositories`, `{project}.webapi.endpoints.{feature}`)
- **Entity pattern**: Inherits `AbstractDomainObject`, all properties `virtual`, two constructors (empty + parameterized), `GetValidator()` overridden
- **Use case pattern**: Outer class with nested `Command` and `Handler`, implements `ICommand<Result<T>>` / `ICommandHandler<TCommand, TResult>`
- **Repository pattern**: Interface in Domain, implementation in Infrastructure. `IRepository<T, Guid>` for write, `IReadOnlyRepository<T, Guid>` for read-only
- **Endpoint pattern**: Inherits `BaseEndpoint<TRequest, TResponse>`, proper HTTP verb configuration, error handling via `ExceptionalError` switch, FluentValidation request validator exists for each endpoint

> For complete architecture rules, layer structure, and violation examples, see `references/architecture-rules.md`

### Step 3: Review Code Quality

Validate C# conventions and naming:
- Naming: PascalCase for classes/methods/properties, camelCase for local variables/parameters, `I` prefix for interfaces
- Namespace matches folder structure: `{project}.{layer}.{sublayer}`
- `virtual` keyword on all entity and DAO properties (NHibernate requirement)
- Proper use of `sealed` for value objects and records
- No `var` for ambiguous types where the type is not obvious from the right-hand side
- XML documentation (`///`) on public APIs and complex methods
- All comments and documentation in English
- No hardcoded strings (use resource classes for constants)
- No magic numbers without named constants
- No commented-out code without reason, no TODOs without ticket reference
- Primary constructor injection for handlers and services (C# 12+)
- Date handling: request models use `DateTimeOffset`, AutoMapper converts to UTC (`.UtcDateTime`), entities store `DateTime` in UTC, queries compare against `DateTime.UtcNow` (never `DateTime.Now`)
- Do NOT flag formatting issues already handled by dotnet format or IDE settings

> For complete C# rules, naming conventions, and patterns, see `references/csharp-rules.md`

### Step 4: Review Data Access Patterns

Validate data access solution matches the operation type:
- **CRUD operations -> IRepository<T, Guid>** (Add, Save, Delete with transactions)
- **Read-only queries -> IReadOnlyRepository<T, Guid> with DAOs** (no transactions, flattened POCOs)
- **Paginated lists -> GetManyAndCountAsync** with proper SortingCriteria
- NHibernate mappers: correct `ClassMapping<T>` configuration (Schema, Table, Id, Properties, Relations)
- DAO mappers: `Mutable(false)` mandatory, maps to database VIEW, `SearchAll` via `CONCAT_WS`
- Unit of Work: lazy repository creation, proper `Dispose` pattern (transaction first, then session)
- Transaction management: `BeginTransaction` -> operation -> `Commit` on success, `Rollback` in catch
- No direct SQL in repositories unless justified (use HQL/NHibernate API)
- `Add` for new entities, `Save` for existing entities (never mix them)
- Proper cascade configuration for relationships (Many-to-One, One-to-Many, Many-to-Many)
- Async/await: no blocking calls (`.Result`, `.Wait()`), `CancellationToken` propagated, N+1 queries prevented with eager loading or separate queries

> For complete data access rules, mapper patterns, and repository guidelines, see `references/data-access-rules.md`

### Step 5: Review Anti-Patterns

Check for these violations (Critical unless noted):
- No business logic in handlers (belongs in domain entities/validators)
- No persistence logic in domain layer (no ORM attributes, no repositories)
- No `Result.Fail()` in GetManyAndCount handlers (rethrow instead)
- No missing `Rollback()` in catch blocks after `BeginTransaction()`
- No missing `virtual` on entity/DAO properties (NHibernate proxy failure)
- No `createApi()` equivalent: no direct `ISession` usage in handlers (use repositories)
- (IMPORTANTE) No missing `WithMessage()` or `WithErrorCode()` on validator rules
- (IMPORTANTE) No wrong exception catch order (specific before generic)
- (IMPORTANTE) No missing tag registration equivalents (repository not registered in IUnitOfWork)
- (MENOR) No missing `Mutable(false)` on DAO mappers

> For the complete anti-patterns catalog with BAD/GOOD code examples, see `references/anti-patterns-catalog.md`

### Step 6: Review Error Handling & Security

Check for error handling and security issues in changed files:

- **Error Handling:** FluentResults `Result<T>` / `Result` used consistently, proper exception-to-Result conversion in handlers, correct HTTP status mapping in endpoints (400/404/409/500), proper catch block order (specific to generic)
- **Security:** No hardcoded credentials or connection strings, parameterized queries (no SQL injection), input validation via FluentValidation, no sensitive data in logs, proper authentication/authorization on endpoints
- **Migrations:** Reversible migrations (Down method implemented), no destructive operations without review, idempotent seeds, no secrets in seed data

> For the complete error handling and security checklists, see `references/review-process.md`

### Step 7: Generate Peer Review Report

Produce a markdown report following this structure. The report is written **entirely in Spanish**.

```markdown
# Peer Review: {branch-name}

> **Generado con:** review-pr-code v1.0.0
> **Fecha:** YYYY-MM-DD

---

**Revisor:** Claude Code
**Estado:** Aprobado | Aprobado con observaciones | Necesita correcciones

## Resumen Ejecutivo

[1-3 sentences: overall quality assessment, main changes, key findings]

## Informacion del Branch

- **Branch:** {branch-name}
- **Base:** {detected-base-branch}
- **Commits:** N
- **Archivos modificados:** N
- **Lineas:** +N / -N

## Archivos Revisados

| Archivo | Capa | Issues |
|---------|------|--------|
| `src/{project}.domain/entities/User.cs` | Domain | N |
| `src/{project}.application/usecases/users/CreateUserUseCase.cs` | Application | N |

## Issues Encontrados

### Criticos

Ninguno.

_(Or list issues using the structure below)_

### Importantes

#### Issue #N: [Short descriptive title]

- **Archivo:** `path/to/file.cs:line-numbers`
- **Tipo:** [Architecture / Domain / Application / Infrastructure / WebApi / Migration / Security / etc.]
- **Descripcion:** [What is wrong and why it matters]
- **Guia:** `path/to/relevant/guide.md` _(optional, only if a project guide applies)_
- **Sugerencia:**

\```csharp
// Codigo actual
[problematic code]

// Codigo sugerido
[fixed code]
\```

### Menores

[Same structure. If none: "Ninguno."]

## Checklist de Cumplimiento

| Capa | Status | Observaciones |
|------|--------|---------------|
| Domain | OK/Observaciones/Requiere cambios | [brief note] |
| Infrastructure | OK/Observaciones/Requiere cambios | |
| Application | OK/Observaciones/Requiere cambios | |
| WebAPI | OK/Observaciones/Requiere cambios | |
| Migrations | OK/N/A | |
| Tests | OK/Observaciones | |

> Use N/A when no files were modified in a layer. Add a note below the table when an observation refers to a pre-existing issue not introduced by the branch.

## Aspectos Positivos

- **[Bold title]**: [Detailed explanation of what was done well and why it matters]

## Conclusion

[Brief paragraph: overall assessment, whether issues are blocking or not. End with concrete next steps if any actions are required, e.g., "Proximos pasos: (1) corregir X, (2) agregar Y." If no actions: "El branch esta listo para merge."]
```

For Criticos and Importantes issues, always include code snippets showing current vs suggested code.

### Step 8: Save Report

Save the generated report as a markdown file in the project's `.claude/reviews/` directory:

```
.claude/reviews/{branch-name}-review.md
```

- Create the `.claude/reviews/` directory if it does not exist.
- The file name uses the branch name (e.g., `kc-350-users-crud-backend-review.md`).
- If a report already exists for the same branch, overwrite it with the new version.

> For the full report template with detailed examples and edge cases, see `references/review-process.md`

### Severity Definitions

| Severidad | Significado | Accion |
|-----------|------------|--------|
| **CRITICO** | Rompe arquitectura, causa bugs, viola reglas core | Bloquea aprobacion -- debe corregirse antes del merge |
| **IMPORTANTE** | Code smell, desviacion de convenciones, riesgo de mantenibilidad | Debe corregirse antes del merge |
| **MENOR** | Podria mejorarse pero no es incorrecto | Sugerencia para considerar |

### Report Status Logic

| Condicion | Estado |
|-----------|--------|
| Sin issues o solo MENORes triviales | **Aprobado** |
| Tiene IMPORTANTEs y/o MENORes, sin CRITICOs | **Aprobado con observaciones** |
| Tiene al menos un CRITICO | **Necesita correcciones** |

> For the complete review process, common scenarios, and review tips, see `references/review-process.md`

---

## References

For complete rules, code examples, and edge cases, consult files in `references/`:

| Topic | Reference |
|-------|-----------|
| Architecture Rules | `references/architecture-rules.md` |
| C# Rules | `references/csharp-rules.md` |
| Data Access Rules | `references/data-access-rules.md` |
| Anti-Patterns Catalog | `references/anti-patterns-catalog.md` |
| Review Process | `references/review-process.md` |

---

## Examples

### Example 1: Review a pull request

User says: "Review this PR" or "Review the users CRUD"

Actions:
1. Detect the base branch via merge-base distance (finds closest: e.g. `devel`)
2. Run `git diff --name-status devel...HEAD` to identify changed files in the PR
3. Run environment verification (build, tests) and note pre-existing vs new issues
4. Read each changed file, classifying by layer (Domain, Application, Infrastructure, WebApi, Migration)
5. Check architecture: layer boundaries, project structure, correct patterns per layer
6. Check code quality: naming conventions, C# conventions, documentation
7. Check data access: mapper configuration, repository patterns, transaction management
8. Check anti-patterns: no logic leaks between layers, proper error handling
9. Generate Peer Review report with issues grouped by severity (CRITICO/IMPORTANTE/MENOR) and an Estado verdict

Result: Markdown Peer Review report with numbered issues, compliance checklist (Por Capa + General), positive aspects, and Accion Requerida.

### Example 2: Quick quality check on a single file

User says: "Check quality of CreateUserUseCase.cs"

Actions:
1. Read the file content
2. Verify it follows Command/Handler pattern (outer class with nested Command and Handler)
3. Check primary constructor injection, FluentResults usage, transaction management
4. Check error handling (catch block order, Result conversion, exception types)
5. Check naming conventions (namespace, class name, method signatures)
6. Report findings

Result: Focused review report for the single file.

### Example 3: Validate a new feature was scaffolded correctly

User says: "Validate the products feature across all layers"

Actions:
1. List all files related to the products feature across Domain, Application, Infrastructure, WebApi
2. Verify entity has correct structure (AbstractDomainObject, virtual, constructors, validator)
3. Verify use cases follow Command/Handler pattern with proper error handling
4. Verify mapper configuration (ClassMapping, schema, table, properties)
5. Verify repository implements IRepository and is registered in IUnitOfWork
6. Verify endpoints use BaseEndpoint with correct HTTP verbs and error mapping
7. Verify DTOs, request/response models, and AutoMapper profiles
8. Check for missing components (tests, migrations)
9. Report missing or misconfigured elements

Result: Architecture compliance report with a checklist of what passes and what needs fixing.

---

## Troubleshooting

### Review finds too many false positives

Cause: Reviewing files outside the feature scope or applying wrong layer rules.
Solution: First classify the layer and component type. Only apply rules relevant to that layer. A migration file should not be checked against entity rules.

### Cannot determine which files changed

Cause: Working directly on main/master/develop, or no common base branch exists locally.
Solution: If on a base branch, there is no PR to review -- ask the user which commits to review or which files to check. If no base branch is found, ask the user to specify which branch the PR targets.

### Skill detects the wrong base branch

Cause: The merge-base distance heuristic may produce incorrect results when branches have complex merge histories or when a feature branch has been rebased.
Solution: The user can specify the base branch explicitly (e.g., "Review this PR against develop"). A user-specified branch always takes precedence over auto-detection.

### Entity has virtual properties but tests fail with proxy errors

Cause: NHibernate requires ALL properties (including navigation) to be `virtual` for proxy generation.
Solution: Flag as Critical if any property in an entity or DAO is missing the `virtual` keyword.

### Handler uses try-catch but misses specific exceptions

Cause: Only catching generic `Exception` instead of domain-specific exceptions first.
Solution: Flag as Important. Catch blocks must follow the order: `InvalidDomainException` -> `DuplicatedDomainException` -> `ResourceNotFoundException` -> `Exception` (generic last).

### GetManyAndCount handler returns Result.Fail on error

Cause: Incorrect error handling pattern for list operations.
Solution: GetManyAndCount handlers should `Rollback()` then `throw;` in catch blocks, NOT return `Result.Fail()`. The endpoint layer handles the exception.

### Migration has Up() but no Down()

Cause: Developer forgot to implement the reverse migration.
Solution: Flag as Important. All migrations should be reversible with a proper `Down()` method unless the migration is truly irreversible (document why).

### Repository uses Add() for an existing entity

Cause: Confusion between `Add` (INSERT) and `Save` (UPDATE).
Solution: Flag as Critical. Using `Add()` on an existing entity causes `StaleObjectStateException`. Use `Save()` for updates.

---

## Related

- **Domain Layer:** `create-domain` -- Entities, validators, value objects, exceptions, DAOs, repository interfaces
- **Application Layer:** `create-use-case` -- Use cases, Command/Handler pattern, error handling
- **Infrastructure Layer:** `create-repository` -- NHibernate mappers, repository implementations
- **WebApi Layer:** `create-endpoint` -- Endpoints, DTOs, request/response models
- **Migrations:** `create-migration` -- FluentMigrator database migrations
- **Submit PR Review:** `submit-pr-review` (in `skills/review/`, install via `init review`) -- Reads the markdown report this skill produces in `.claude/reviews/` and posts the issues as inline PR comments on GitHub
- **Verify PR Review:** `verify-pr-review` (in `skills/review/`, install via `init review`) -- After comments are posted, verifies whether they were resolved and produces a final review verdict
