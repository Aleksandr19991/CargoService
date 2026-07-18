# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Cargo shipping platform being built as a **.NET 10 / C# microservices monorepo** (PostgreSQL per service, RabbitMQ for inter-service events — RabbitMQ integration is not wired up yet, Serilog → Seq for centralized logging). The full target architecture, the list of microservices, their entities/APIs/events, and the development backlog (organized in phases) are specified in [spec.md](spec.md) — read it before planning cross-service work or adding a new microservice, and keep its Фаза 0 checklist in [spec.md](spec.md) updated as repo-infrastructure tasks are completed.

Only **identity-service** has actual code today; every other service under `services/` is a placeholder folder with a `README.md` pointing at its section of `spec.md`.

## Repository layout

```
services/{service-name}/     one microservice per folder (see spec.md §2 for the full list)
shared/CargoService.Contracts/   shared library of v1 RabbitMQ event DTOs (Events/V1/), not yet referenced by any service project
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

Logging is Serilog, wired via `builder.Host.UseSerilog(...)` in `Program.cs` (not the default `Logging` config section — appsettings use a `Serilog`/`MinimumLevel` section instead). It always writes to console; it additionally writes to Seq when `Seq:ServerUrl` is set (`http://localhost:5341` in `appsettings.Development.json` for local runs, overridden to `http://seq:80` via the `Seq__ServerUrl` env var in `docker-compose.yml` for the containerized network). New services should copy this same `UseSerilog` block plus the `Seq:ServerUrl` config convention rather than inventing a different logging setup.

## identity-service

Path: `services/identity-service/`. Solution file: `IdentityService.slnx` (lives inside the service folder, not at repo root — there is currently no repo-wide solution).

Projects: `IdentityService.Domain`, `IdentityService.Application`, `IdentityService.Infrastructure`, `IdentityService.Persistence`, `IdentityService` (API/host). Current scope: CRUD over a single `User` entity (register/update/delete/get by id/get all) via `UsersController` at `api/users`. `User` has no password field — credentials live entirely in Keycloak (see below).

### Keycloak is the identity provider

Auth is delegated to **Keycloak**, not self-issued. `docker-compose.yml`'s `keycloak` service imports `docker/keycloak/realm-export.json` on every start (`start-dev --import-realm`) — that file is the source of truth for the realm (`cargoservice`), its 5 realm roles (matching `IdentityService.Domain.Enums.Role`), and the `identity-service` confidential client (service account with `manage-users` on `realm-management`, `directAccessGrantsEnabled` for a future ROPC login proxy). Edit that JSON, not the admin console, to change realm/client config — console changes don't survive a container recreate.

- **Token validation** (`Program.cs`): `AddJwtBearer` uses `Keycloak:BaseUrl`/`Realm`/`ClientId` to build the OIDC metadata address and validates against `Keycloak:ValidIssuer`. These are deliberately separate: `BaseUrl` is whatever URL actually reaches Keycloak over HTTP (`http://localhost:8080` locally, overridden to `http://keycloak:8080` via the `Keycloak__BaseUrl` env var when both run in `docker-compose`), while `ValidIssuer` is always `http://localhost:8080/realms/cargoservice` because that's what `KC_HOSTNAME: localhost` (in `docker-compose.yml`) stamps into every token's `iss` claim regardless of how it was reached. If you ever change `KC_HOSTNAME`, `ValidIssuer` must change with it.
- **Role claims**: Keycloak puts realm roles in a `realm_access.roles` JSON claim, not individual claims. An `OnTokenValidated` handler in `Program.cs` copies the ones matching the `Role` enum onto `ClaimTypes.Role` so `[Authorize(Roles = ...)]` works normally.
- **User provisioning**: `IIdentityProviderClient` (interface in `IdentityService.Application`, implementation `KeycloakIdentityProviderClient` in `IdentityService.Infrastructure/Keycloak/`) creates the user in Keycloak via the Admin REST API (client-credentials grant using the service account) and assigns the realm role. `UsersService.CreateUserAsync` calls it *before* writing the local row, using the id Keycloak generates as `User.Id` — so an identity-service `User.Id` and the Keycloak user id are always the same GUID. There's no rollback if the local DB write fails after the Keycloak call succeeds (orphaned Keycloak user) — acceptable for now, revisit if it becomes a real problem.
- `POST api/users/register` stays `[AllowAnonymous]` and always creates a `Client`; `POST api/users/staff` (`Admin`-only) is how non-Client accounts get created — both go through the same Keycloak-provisioning path.

`AuthController` (`[AllowAnonymous]`) proxies to Keycloak's token endpoint via `IIdentityProviderClient`, both returning `{ accessToken, refreshToken, expiresIn, tokenType }` or `401`: `POST api/auth/login` (`AuthenticateAsync`, ROPC grant) and `POST api/auth/refresh` (`RefreshAsync`, `grant_type=refresh_token`). For manual testing, the seeded `admin@cargoservice.local` / `admin` user from `realm-export.json` works against `login`.

## Commands

Build/run the identity-service (from `services/identity-service/`):
```
dotnet build IdentityService.slnx
dotnet run --project IdentityService
```

Full local stack (Postgres + RabbitMQ + MinIO + Seq + Keycloak + identity-service API), from repo root:
```
docker compose up --build
```
`docker-compose.override.yml` is picked up automatically (no `-f` needed) and runs the service via the Dockerfile's `dev` stage (`dotnet watch`, source bind-mounted) instead of the published `final` image — edits to `.cs` files under `services/identity-service/` hot-reload inside the container. Each service's `dev` stage/override entry is added alongside its `Dockerfile` as that service gets built.

EF Core migrations (run from `services/identity-service/IdentityService.Persistence/`, targeting the API project for startup config):
```
dotnet ef migrations add <Name> --startup-project ../IdentityService
dotnet ef database update --startup-project ../IdentityService
```

There are no test projects yet.
