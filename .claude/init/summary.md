# Init Backend — Setup Project Report

## Inputs

| Campo | Valor |
|---|---|
| Nombre del proyecto | reservation.handson.lab.backend |
| Ubicación | D:\sdd-hands-on-lab\reservation-handson-lab\backend |
| Modo | default |
| Fecha/hora inicio | 2026-04-23T00:00:00-06:00 |
| Fecha/hora fin | 2026-04-23T00:00:00-06:00 |
| Status | success |

## Plan (Phase 1)

### Creado
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IReadOnlyRepository.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IRepository.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/IUnitOfWork.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/GetManyAndCountResult.cs`
- `src/reservation.handson.lab.backend.domain/interfaces/repositories/SortingCriteria.cs`
- carpeta `src/reservation.handson.lab.backend.domain/interfaces/repositories/`

### Modificado
- Ninguno. El `.gitignore` ya incluía la línea `.env` (línea 7, del `dotnet new gitignore` del bootstrap original).

### Omitido (preexistente — respetado intacto)
- `reservation.handson.lab.backend.sln`
- `Directory.Packages.props`
- `.gitignore`
- `src/reservation.handson.lab.backend.domain/` + `AbstractDomainObject.cs`, 3 excepciones, carpeta `validators/`
- `src/reservation.handson.lab.backend.application/`
- `src/reservation.handson.lab.backend.infrastructure/`
- `src/reservation.handson.lab.backend.webapi/`
- `tests/reservation.handson.lab.backend.domain.tests/`
- `tests/reservation.handson.lab.backend.application.tests/`
- `tests/reservation.handson.lab.backend.infrastructure.tests/`
- `tests/reservation.handson.lab.backend.webapi.tests/`

### Refrescado (`--force`)
- N/A (sin `--force`)

## Comandos ejecutados

| # | Comando | Exit | Resultado |
|---|---|---|---|
| 1 | `mkdir -p src/.../interfaces/repositories` | 0 | executed |
| 2 | Escribir `IReadOnlyRepository.cs` | 0 | executed |
| 3 | Escribir `IRepository.cs` | 0 | executed |
| 4 | Escribir `IUnitOfWork.cs` | 0 | executed |
| 5 | Escribir `GetManyAndCountResult.cs` | 0 | executed |
| 6 | Escribir `SortingCriteria.cs` | 0 | executed |
| 7 | `dotnet build` | 0 | 8/8 proyectos compilados, 0 errores, 0 warnings |
| 8 | `dotnet test --nologo --no-build` | 0 | 4 test assemblies, 0/0 pruebas (esperado) |

## Archivos creados / modificados / refrescados

- **Creados (5 archivos + 1 carpeta)**:
  - `src/reservation.handson.lab.backend.domain/interfaces/repositories/IReadOnlyRepository.cs`
  - `src/reservation.handson.lab.backend.domain/interfaces/repositories/IRepository.cs`
  - `src/reservation.handson.lab.backend.domain/interfaces/repositories/IUnitOfWork.cs`
  - `src/reservation.handson.lab.backend.domain/interfaces/repositories/GetManyAndCountResult.cs`
  - `src/reservation.handson.lab.backend.domain/interfaces/repositories/SortingCriteria.cs`
- **Modificados**: ninguno
- **Refrescados**: ninguno (sin `--force`)

## Verificación

| Check | Comando | Resultado |
|---|---|---|
| Build | `dotnet build` | exit 0, 0 errores, 0 warnings |
| Test | `dotnet test --nologo --no-build` | exit 0, 4 assemblies sin pruebas (esperado) |

## Warnings y conflictos con guías

> Esta sección documenta problemas detectados en las guías de APSYS durante la corrida. No son bugs del comando; son feedback para corregir aguas arriba.

- **[info] Corrida idempotente de rellenado**: esta ejecución es una re-invocación después del bootstrap original (commit `26dcc4a`). El bootstrap inicial no había creado las 5 interfaces genéricas de repositorio porque la versión previa de `/dotnet:setup-project` aún no las incluía. Esta corrida las llena sin tocar nada más.
- **[info] Composición multi-guía**: este comando toma porciones de tres guías (`00-project-setup/01-project-setup.md`, `01-domain-layer/00-domain-setup.md` y la sección "Setup: crear carpeta y copiar templates" de `01-domain-layer/06-repository-interfaces.md`). El resto del tema 06 (explicación conceptual de cada interfaz, patrones de repos específicos por entidad) queda fuera del scope del comando.
- **[guide-conflict] Template `Directory.Packages.props` vs guía**: el template referencia `Moq` pero ninguna guía lo instala. El catálogo embebido en este comando NO incluye `Moq` para mantenerse literal a la guía.
- **[template-orphan]** Los siguientes templates existen en el repo de guías pero ningún paso de setup los instala: `webapi/Program.cs` (APSYS custom), `webapi/.env`, `webapi/InternalsVisibleTo.cs`, `tests/DomainTestBase.cs`, `tests/ApplicationTestBase.cs`.

## Next steps

- Todavía no existe `/dotnet:setup-database` ni `/dotnet:setup-persistence`. Cuando se agreguen, la secuencia sugerida será: setup-project → setup-database → setup-persistence.
- Revisa `.claude/init/summary.md` y el diff de `git status` — debería mostrar sólo los 5 archivos nuevos bajo `interfaces/repositories/`.
- El bootstrap inicial (commit `26dcc4a`) sigue válido; esta corrida lo completa.
