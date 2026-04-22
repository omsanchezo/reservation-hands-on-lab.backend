# Init Backend — Setup Project Report

## Inputs

| Campo | Valor |
|---|---|
| Nombre del proyecto | reservation.handson.lab.backend |
| Ubicación | D:\sdd-hands-on-lab\reservation-handson-lab\backend |
| Fecha/hora inicio | 2026-04-22T12:09:47-06:00 |
| Fecha/hora fin | 2026-04-22T12:16:34-06:00 |
| Status | success |

## Plan (Phase 1)

### Creado
- `reservation.handson.lab.backend.sln`
- `Directory.Packages.props`
- `.gitignore` (via `dotnet new gitignore`, ya trae `.env` en línea 7)
- `src/reservation.handson.lab.backend.domain/`
  - `entities/AbstractDomainObject.cs`
  - `entities/validators/` (carpeta vacía)
  - `exceptions/InvalidDomainException.cs`
  - `exceptions/ResourceNotFoundException.cs`
  - `exceptions/DuplicatedDomainException.cs`
- `src/reservation.handson.lab.backend.application/`
- `src/reservation.handson.lab.backend.infrastructure/`
- `src/reservation.handson.lab.backend.webapi/`
- `tests/reservation.handson.lab.backend.domain.tests/`
- `tests/reservation.handson.lab.backend.application.tests/`
- `tests/reservation.handson.lab.backend.infrastructure.tests/`
- `tests/reservation.handson.lab.backend.webapi.tests/`
- `.claude/init/summary.md`

### Modificado
- Ninguno. El `.gitignore` generado ya incluye `.env`; no requirió modificación.

### Omitido
- Ninguno (bootstrap limpio).

## Comandos ejecutados

| # | Comando | Exit | Resultado |
|---|---|---|---|
| 1 | `dotnet new sln -n reservation.handson.lab.backend` | 0 | OK |
| 2 | `dotnet new gitignore` | 0 | OK |
| 3 | `dotnet new classlib -n {name}.domain -o src/...` | 0 | OK |
| 4 | `dotnet sln add src/...domain.csproj` | 0 | OK |
| 5 | `dotnet add src/...domain package FluentValidation` | 0 | OK |
| 6 | `dotnet new nunit -n {name}.domain.tests -o tests/...` | 1 (esperado: NU1008) | Corregido tras strip de `Version=` |
| 7 | `dotnet sln add tests/...domain.tests.csproj` | 0 | OK |
| 8 | `dotnet add tests/...domain.tests package AutoFixture.AutoMoq` | 0 | OK |
| 9 | `dotnet add tests/...domain.tests package FluentAssertions` | 0 | OK |
| 10 | `dotnet add tests/...domain.tests package Castle.Core` | 0 | OK |
| 11 | `dotnet add tests/...domain.tests reference src/...domain` | 0 | OK |
| 12 | `dotnet new classlib -n {name}.application -o src/...` | 0 | OK |
| 13 | `dotnet sln add src/...application.csproj` | 0 | OK |
| 14 | `dotnet add src/...application reference src/...domain` | 0 | OK |
| 15 | `dotnet new nunit -n {name}.application.tests -o tests/...` | 1 (esperado: NU1008) | Corregido tras strip de `Version=` |
| 16 | `dotnet sln add tests/...application.tests.csproj` | 0 | OK |
| 17 | `dotnet add tests/...application.tests package {AutoFixture,Fluent,Castle}` | 0 | OK |
| 18 | `dotnet add tests/...application.tests reference src/...{domain,application}` | 0 | OK |
| 19 | `dotnet new classlib -n {name}.infrastructure -o src/...` | 0 | OK |
| 20 | `dotnet sln add src/...infrastructure.csproj` | 0 | OK |
| 21 | `dotnet add src/...infrastructure reference src/...domain` | 0 | OK |
| 22 | `dotnet new nunit -n {name}.infrastructure.tests -o tests/...` | 1 (esperado: NU1008) | Corregido tras strip de `Version=` |
| 23 | `dotnet sln add tests/...infrastructure.tests.csproj` | 0 | OK |
| 24 | `dotnet add tests/...infrastructure.tests package {AutoFixture,Castle,Fluent}` | 0 | OK |
| 25 | `dotnet add tests/...infrastructure.tests reference src/...{domain,infrastructure}` | 0 | OK |
| 26 | `dotnet new web -n {name}.webapi -o src/...` | 0 | OK |
| 27 | `dotnet sln add src/...webapi.csproj` | 0 | OK |
| 28 | `dotnet add src/...webapi reference src/...{domain,application,infrastructure}` | 0 | OK |
| 29 | `dotnet new nunit -n {name}.webapi.tests -o tests/...` | 1 (esperado: NU1008) | Corregido tras strip de `Version=` |
| 30 | `dotnet sln add tests/...webapi.tests.csproj` | 0 | OK |
| 31 | `dotnet add tests/...webapi.tests package {Mvc.Testing,FluentAssertions}` | 0 | OK |
| 32 | `dotnet add tests/...webapi.tests reference src/...{webapi,domain}` | 0 | OK |
| 33 | `dotnet build` | 0 | 8/8 proyectos compilados |
| 34 | `dotnet test --nologo` | 0 | 4 test assemblies ejecutados, 0 pruebas (esperado) |

> Los "exit 1" en los 4 `dotnet new nunit` son el comportamiento esperado por NU1008 cuando Central Package Management está activo y la plantilla genera `PackageReference Version="..."`. El proyecto queda agregado a la solución pese al error de restauración; se corrige stripeando los atributos `Version=` del csproj generado y corriendo los `dotnet add package` siguientes, que invocan restore implícito.

## Archivos creados

```
reservation.handson.lab.backend.sln
Directory.Packages.props
.gitignore
src/reservation.handson.lab.backend.domain/
  reservation.handson.lab.backend.domain.csproj
  entities/AbstractDomainObject.cs
  entities/validators/
  exceptions/InvalidDomainException.cs
  exceptions/ResourceNotFoundException.cs
  exceptions/DuplicatedDomainException.cs
src/reservation.handson.lab.backend.application/
  reservation.handson.lab.backend.application.csproj
src/reservation.handson.lab.backend.infrastructure/
  reservation.handson.lab.backend.infrastructure.csproj
src/reservation.handson.lab.backend.webapi/
  reservation.handson.lab.backend.webapi.csproj
  Program.cs (generado por template `web`)
  appsettings.json (generado por template `web`)
  appsettings.Development.json (generado por template `web`)
  Properties/launchSettings.json (generado por template `web`)
tests/reservation.handson.lab.backend.domain.tests/
  reservation.handson.lab.backend.domain.tests.csproj
tests/reservation.handson.lab.backend.application.tests/
  reservation.handson.lab.backend.application.tests.csproj
tests/reservation.handson.lab.backend.infrastructure.tests/
  reservation.handson.lab.backend.infrastructure.tests.csproj
tests/reservation.handson.lab.backend.webapi.tests/
  reservation.handson.lab.backend.webapi.tests.csproj
.claude/init/summary.md
```

## Verificación

| Check | Comando | Resultado |
|---|---|---|
| Build | `dotnet build` | exit 0, 0 errores, 0 warnings |
| Test | `dotnet test --nologo` | exit 0, 0/0 (no hay tests aún, esperado) |

## Warnings y conflictos con guías

> Esta sección documenta problemas detectados en las guías de APSYS durante la corrida. No son bugs del comando; son feedback para corregir aguas arriba.

- **[template-conflict] `dotnet new nunit` emite `PackageReference Version=` incompatibles con Central Package Management.** Cada uno de los 4 test projects produjo un error NU1008 en la restauración post-creación; el remedio (strip de `Version=`) ya está embebido en el comando, pero la guía debería anticiparlo como **paso obligatorio** en lugar de implícito. Sugerencia: documentar el flujo "crear nunit → editar csproj → correr `dotnet add package`".
- **[transitive-dep-noise] NU1603 sobre `System.ComponentModel.TypeConverter`**. Durante el restore de los proyectos de test, NuGet intenta resolver una versión vieja de `Castle.Core 4.0.0` (traída como dependencia transitiva de `AutoFixture.AutoMoq`) antes de aplicar nuestro `Castle.Core 5.1.1` del catálogo central. Termina resolviendo `System.ComponentModel.TypeConverter 4.1.0` sin pérdida funcional, pero genera ruido en el log. Es inocuo.
- **[guide-conflict] Domain sin interfaces de repositorio**: `01-domain-layer/00-domain-setup.md` no copia `IRepository`, `IReadOnlyRepository`, `IUnitOfWork`, `GetManyAndCountResult`, `SortingCriteria` aunque los templates existen. Un futuro `/dotnet:setup-persistence` fallará al compilar hasta que la guía las incluya.
- **[guide-conflict] Template `Directory.Packages.props` vs guía**: el template referencia `Moq` pero ninguna guía lo instala. El catálogo embebido en este comando NO incluye `Moq` para mantenerse literal a la guía.
- **[template-orphan]** Los siguientes templates existen en el repo de guías pero ningún paso de setup los instala: `webapi/Program.cs` (APSYS custom), `webapi/.env`, `webapi/InternalsVisibleTo.cs`, `tests/DomainTestBase.cs`, `tests/ApplicationTestBase.cs`. El `Program.cs` actual es el que genera `dotnet new web`.

## Next steps

- Todavía no existe `/dotnet:setup-database` ni `/dotnet:setup-persistence`. Cuando se agreguen, la secuencia sugerida será: setup-project → setup-database → setup-persistence.
- Revisa `.claude/init/summary.md` antes de hacer el primer commit.
