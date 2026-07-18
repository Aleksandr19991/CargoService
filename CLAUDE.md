# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Cargo shipping platform being built as a **.NET 10 / C# microservices monorepo** (PostgreSQL per service, RabbitMQ for inter-service events — RabbitMQ integration is not wired up yet). The full target architecture, the list of microservices, their entities/APIs/events, and the development backlog (organized in phases) are specified in [spec.md](spec.md) — read it before planning cross-service work or adding a new microservice, and keep its Фаза 0 checklist in [spec.md](spec.md) updated as repo-infrastructure tasks are completed.

Only **identity-service** has actual code today; every other service under `services/` is a placeholder folder with a `README.md` pointing at its section of `spec.md`.

## Repository layout

```
services/{service-name}/     one microservice per folder (see spec.md §2 for the full list)
shared/CargoService.Contracts/   shared library for RabbitMQ event DTOs (empty scaffold so far)
spec.md                      architecture spec + phased backlog — source of truth for what to build next
Directory.Build.props        common MSBuild properties (TargetFramework, Nullable, ImplicitUsings, LangVersion) for every project in the repo
Directory.Packages.props     central package management — package versions are pinned here; csproj files reference packages without a Version attribute
.editorconfig                shared C# style/formatting/naming rules for all services
```

Each microservice follows Clean Architecture with one project per layer, named `{ServiceName}.{Layer}`:
- `{ServiceName}.Domain` — entities only, no dependencies.
- `{ServiceName}.Application` — use-case services + interfaces (e.g. `IUsersService`, `IUsersRepository`), depends on Domain.
- `{ServiceName}.Persistence` — EF Core `DbContext`, `IEntityTypeConfiguration<T>` classes (auto-discovered via `ApplyConfigurationsFromAssembly`), repository implementations, EF migrations. Depends on Application + Domain.
- `{ServiceName}` (no suffix) — the ASP.NET Core Web API host/controllers project. Depends on Application + Persistence.

Wiring between layers is done via `IServiceCollection` extension methods, not inline in `Program.cs`:
- `AddApplicationServices()` (in `{ServiceName}.Application/Configuration/ServicesConfiguration.cs`) registers Application-layer services.
- `AddPersistence(connectionString)` (in `{ServiceName}.Persistence/Configuration/PersistenceConfiguration.cs`) registers the `DbContext` (Npgsql) and repositories.

`Program.cs` calls both, then runs `dbContext.Database.MigrateAsync()` on startup before mapping controllers — migrations are applied automatically when the API boots, there is no separate migration step in normal dev/docker flow.

## identity-service

Path: `services/identity-service/`. Solution file: `IdentityService.slnx` (lives inside the service folder, not at repo root — there is currently no repo-wide solution).

Projects: `IdentityService.Domain`, `IdentityService.Application`, `IdentityService.Persistence`, `IdentityService` (API/host). Current scope: CRUD over a single `User` entity (register/update/delete/get by id/get all) via `UsersController` at `api/users`. Passwords are stored as plain text on `User.Password` — there is no hashing yet.

`docker-compose.yml` (repo root) also provisions a Keycloak container for this service (`KC_*` env vars, `Keycloak__Authority` / `Keycloak__MetadataAddress` passed into the app), but no authentication middleware is wired up in `Program.cs` yet — Keycloak runs alongside the API without being consumed by it.

## Commands

Build/run the identity-service (from `services/identity-service/`):
```
dotnet build IdentityService.slnx
dotnet run --project IdentityService
```

Full local stack (Postgres + Keycloak + identity-service API), from repo root:
```
docker compose up --build
```

EF Core migrations (run from `services/identity-service/IdentityService.Persistence/`, targeting the API project for startup config):
```
dotnet ef migrations add <Name> --startup-project ../IdentityService
dotnet ef database update --startup-project ../IdentityService
```

There are no test projects yet.
