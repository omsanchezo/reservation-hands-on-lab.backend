# Init Backend — Setup Project Report

## Inputs

| Campo | Valor |
|---|---|
| Nombre del proyecto | reservation.handson.lab.backend |
| Ubicación | D:\sdd-hands-on-lab\reservation-handson-lab\backend |
| Modo | default |
| Fecha/hora inicio | 2026-04-24T14:53:xx (local) |
| Fecha/hora fin | 2026-04-24T14:56:xx (local) |
| Status | success |

## Plan (Phase 1)

### Creado

- `reservation.handson.lab.backend.sln`
- `Directory.Packages.props`
- `.gitignore`
- `src/reservation.handson.lab.backend.domain/` + `.csproj` + carpetas `entities/validators/`, `exceptions/`, `interfaces/repositories/`
- `src/reservation.handson.lab.backend.domain/entities/AbstractDomainObject.cs`
- `src/reservation.handson.lab.backend.domain/exceptions/InvalidDomainException.cs`
- `src/reservation.handson.lab.backend.domain/exceptions/ResourceNotFoundException.cs`
- `src/reservation.handson.lab.backend.domain/exceptions/DuplicatedDomainException.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IReadOnlyRepository.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IRepository.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IUnitOfWork.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/GetManyAndCountResult.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/SortingCriteria.cs`
- `src/reservation.handson.lab.backend.application/` + `.csproj`
- `src/reservation.handson.lab.backend.infrastructure/` + `.csproj`
- `src/reservation.handson.lab.backend.webapi/` + `.csproj` + `Program.cs` + `appsettings.json` + `appsettings.Development.json` + `Properties/launchSettings.json` (del template `dotnet new web`)
- `tests/reservation.handson.lab.backend.domain.tests/` + `.csproj`
- `tests/reservation.handson.lab.backend.application.tests/` + `.csproj`
- `tests/reservation.handson.lab.backend.infrastructure.tests/` + `.csproj`
- `tests/reservation.handson.lab.backend.webapi.tests/` + `.csproj`
- `.claude/init/summary.md`

### Modificado

- `.gitignore` — línea `.env` anexada tras `dotnet new gitignore`.

### Omitido (preexistente — respetado intacto)

- `.claude/init/` (directorio preexistente; el summary se escribió dentro).

### Refrescado (`--force`)

- N/A — corrida default.

## Comandos ejecutados

| # | Comando | Exit | Resultado |
|---|---|---|---|
| 1 | `mkdir -p src tests` | 0 | executed |
| 2 | `dotnet new sln -n reservation.handson.lab.backend` | 0 | executed |
| 3 | Write `Directory.Packages.props` | 0 | executed |
| 4 | `dotnet new gitignore` | 0 | executed |
| 5 | `dotnet new classlib -o src/...domain` | 0 | executed |
| 6 | `rm src/...domain/Class1.cs` | 0 | executed |
| 7 | `dotnet sln add src/...domain.csproj` | 0 | executed |
| 8 | `dotnet add src/...domain package FluentValidation` | 0 | executed |
| 9 | `mkdir -p` domain subfolders | 0 | executed |
| 10 | Write 9 domain files (AbstractDomainObject + 3 excepciones + 5 interfaces) | 0 | executed |
| 11 | `dotnet new nunit -o tests/...domain.tests` | 0 | executed (restore falló por NU1008 — esperado) |
| 12 | `rm tests/...domain.tests/UnitTest1.cs` | 0 | executed |
| 13 | `dotnet sln add tests/...domain.tests.csproj` | 0 | executed |
| 14 | Strip `Version=` en `...domain.tests.csproj` | 0 | executed |
| 15 | `dotnet add ...domain.tests package AutoFixture.AutoMoq/FluentAssertions/Castle.Core` | 0 | executed (x3) |
| 16 | `dotnet add ...domain.tests reference ...domain.csproj` | 0 | executed |
| 17 | Stages 3/4/5 análogos (classlib/classlib/web + tests + references + strip Version=) | 0 | executed |
| 18 | Append `.env` a `.gitignore` | 0 | executed |
| 19 | `dotnet build` | 0 | executed — 0 warnings, 0 errors |
| 20 | `dotnet test` | 0 | executed — 0 tests (esperado, UnitTest1.cs removidos) |

## Archivos creados / modificados / refrescados

**Creados** (lista completa):

- `reservation.handson.lab.backend.sln`
- `Directory.Packages.props`
- `.gitignore`
- `src/reservation.handson.lab.backend.domain/reservation.handson.lab.backend.domain.csproj`
- `src/reservation.handson.lab.backend.domain/entities/AbstractDomainObject.cs`
- `src/reservation.handson.lab.backend.domain/exceptions/{InvalidDomainException,ResourceNotFoundException,DuplicatedDomainException}.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/{IReadOnlyRepository,IRepository,IUnitOfWork,GetManyAndCountResult,SortingCriteria}.cs`
- `src/reservation.handson.lab.backend.application/reservation.handson.lab.backend.application.csproj`
- `src/reservation.handson.lab.backend.infrastructure/reservation.handson.lab.backend.infrastructure.csproj`
- `src/reservation.handson.lab.backend.webapi/reservation.handson.lab.backend.webapi.csproj` (+ `Program.cs`, `appsettings*.json`, `Properties/launchSettings.json` del template)
- `tests/reservation.handson.lab.backend.domain.tests/reservation.handson.lab.backend.domain.tests.csproj`
- `tests/reservation.handson.lab.backend.application.tests/reservation.handson.lab.backend.application.tests.csproj`
- `tests/reservation.handson.lab.backend.infrastructure.tests/reservation.handson.lab.backend.infrastructure.tests.csproj`
- `tests/reservation.handson.lab.backend.webapi.tests/reservation.handson.lab.backend.webapi.tests.csproj`
- `.claude/init/summary.md`

**Modificados:**

- `.gitignore` — `.env` anexado.

## Verificación

| Check | Comando | Resultado |
|---|---|---|
| Build | `dotnet build` | exit 0 — 8 proyectos, 0 warnings, 0 errors |
| Test | `dotnet test` | exit 0 — 0 tests ejecutados (proyectos vacíos tras remover `UnitTest1.cs`) |

## Warnings y conflictos con guías

- **[info] NU1603 Castle.Core 4.0.0 transitive**: durante el restore de tests aparece `warn : NU1603: Castle.Core 4.0.0 depende de System.ComponentModel.TypeConverter (>= 4.0.1), pero no se encontró System.ComponentModel.TypeConverter 4.0.1. System.ComponentModel.TypeConverter 4.1.0 se resolvió en su lugar.` Es un warning transitivo benigno — NuGet resuelve una versión ligeramente superior compatible. El `Directory.Packages.props` pinea `Castle.Core 5.1.1` como versión directa, así que tu código de tests usa 5.1.1; el warning sólo refleja la cadena transitiva de otro paquete (probablemente AutoFixture.AutoMoq). No bloquea.
- **[behavior] `dotnet new nunit` genera `PackageReference Version="..."`**: incompatible con Central Package Management (NU1008). El comando maneja esto strippeando `Version=` de cada `PackageReference` después de `dotnet new nunit`. El restore post-creación falla, pero el proyecto se agrega al `.sln` correctamente y se repara en el step siguiente.
- **[info] Composición multi-guía**: este comando toma porciones de tres guías (`00-project-setup/01-project-setup.md`, `01-domain-layer/00-domain-setup.md` y la sección "Setup: crear carpeta y copiar templates" de `01-domain-layer/06-repository-interfaces.md`). No es un conflicto — es el modelo de composición descrito en Decisión #8 del `design.md`.

## Next steps

- Todavía no existe `/dotnet:setup-database` ni `/dotnet:setup-persistence`. Cuando se agreguen, la secuencia sugerida será: setup-project → setup-database → setup-persistence.
- Revisa `D:\sdd-hands-on-lab\reservation-handson-lab\backend\.claude\init\summary.md` antes de hacer el primer commit.
- El historial git tenía los mismos archivos committed como "deleted" en el working tree. Antes de commitear, considera resetear `git add -A` para dejar todo limpio (el nuevo árbol debería coincidir con el estado previo al rm, módulo cambios de template).
