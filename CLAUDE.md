# CLAUDE.md

Этот файл содержит инструкции для Claude Code (claude.ai/code) при работе с кодом в этом репозитории.

## Обзор проекта

Платформа грузоперевозок, разрабатываемая как **монорепозиторий микросервисов на .NET 10 / C#** (PostgreSQL на каждый сервис, RabbitMQ для межсервисных событий через транзакционный outbox, Serilog → Seq для централизованного логирования). Полная целевая архитектура, список микросервисов, их сущности/API/события и бэклог разработки (по фазам) описаны в [spec.md](spec.md) — читайте его перед планированием кросс-сервисной работы или добавлением нового микросервиса, и держите его чек-лист Фазы 0 в [spec.md](spec.md) актуальным по мере выполнения задач по инфраструктуре репозитория.

Реальный код сегодня есть только у **identity-service**; каждый остальной сервис под `services/` — это папка-заглушка с `README.md`, указывающим на соответствующий раздел `spec.md`.

## Структура репозитория

```
services/{service-name}/     один микросервис на папку (полный список — spec.md §2)
shared/CargoService.Contracts/   общая библиотека DTO событий RabbitMQ v1 (Events/V1/) и конвенций топологии (Messaging/RabbitMqConventions.cs)
spec.md                      архитектурная спецификация + бэклог по фазам — источник истины о том, что строить дальше
Directory.Build.props        общие MSBuild-свойства (TargetFramework, Nullable, ImplicitUsings, LangVersion) для всех проектов репозитория
Directory.Packages.props     централизованное управление пакетами — версии пакетов закреплены здесь; csproj-файлы ссылаются на пакеты без атрибута Version
.editorconfig                общие правила стиля/форматирования/именования C# для всех сервисов
```

Каждый микросервис построен по Clean Architecture с одним проектом на слой, названным `{ServiceName}.{Layer}`:
- `{ServiceName}.Domain` — только сущности, без зависимостей.
- `{ServiceName}.Application` — сервисы сценариев использования + интерфейсы (например, `IUsersService`, `IUsersRepository`), зависит от Domain.
- `{ServiceName}.Persistence` — EF Core `DbContext`, классы `IEntityTypeConfiguration<T>` (автоматически обнаруживаются через `ApplyConfigurationsFromAssembly`), реализации репозиториев, EF-миграции. Зависит от Application + Domain.
- `{ServiceName}` (без суффикса) — проект хоста/контроллеров ASP.NET Core Web API. Зависит от Application + Persistence.

Связывание между слоями выполняется через extension-методы `IServiceCollection` (и, в API-проекте, `WebApplicationBuilder`/`WebApplication`), а не инлайном в `Program.cs` — `Program.cs` должен оставаться коротким манифестом вызовов, вся логика уходит в extension-методы под `Configuration/` каждого проекта:
- `AddApplicationServices()` (в `{ServiceName}.Application/Configuration/ServicesConfiguration.cs`) регистрирует сервисы слоя Application.
- `AddPersistence(connectionString)` (в `{ServiceName}.Persistence/Configuration/PersistenceConfiguration.cs`) регистрирует `DbContext` (Npgsql) и репозитории.
- В самом API-проекте (`{ServiceName}/Configuration/`) — свои extension-методы на `WebApplicationBuilder`/`IServiceCollection`/`WebApplication` под каждую заботу (логирование, аутентификация, регистрация контроллеров/валидации/маппинга, миграция БД при старте, dev-only эндпоинты). См. identity-service ниже как образец для новых сервисов.

`Program.cs` вызывает эти extension-методы, затем при старте мигрирует БД перед маппингом контроллеров — миграции применяются автоматически при запуске API, отдельного шага миграции в обычном dev/docker-процессе нет.

Логирование через Serilog, подключается extension-методом на `WebApplicationBuilder` (`AddSerilogLogging()` в `Configuration/LoggingConfiguration.cs` identity-service — не через стандартную секцию конфигурации `Logging`, appsettings используют секцию `Serilog`/`MinimumLevel`). Логи всегда пишутся в консоль; дополнительно пишутся в Seq, если задан `Seq:ServerUrl` (`http://localhost:5341` в `appsettings.Development.json` для локальных запусков, переопределяется на `http://seq:80` через переменную окружения `Seq__ServerUrl` в `docker-compose.yml` для контейнерной сети). Новые сервисы должны копировать этот же блок `UseSerilog` и конвенцию конфигурации `Seq:ServerUrl`, а не изобретать собственную настройку логирования.

## identity-service

Путь: `services/identity-service/`. Файл решения: `IdentityService.slnx` (лежит внутри папки сервиса, а не в корне репозитория — репозиторного решения на весь монорепо пока не существует).

Проекты: `IdentityService.Domain`, `IdentityService.Application`, `IdentityService.Infrastructure`, `IdentityService.Persistence`, `IdentityService` (API/хост). Текущий охват: CRUD над одной сущностью `User` (регистрация/обновление/удаление/получение по id/получение всех) через `UsersController` на `api/users`. У `User` нет поля пароля — учётные данные целиком живут в Keycloak (см. ниже).

### API-слой: контроллеры, маппинг, валидация

Конвенция для `IdentityService` (API-проект), обязательная для любого нового эндпоинта:

- **Контроллеры** — только в `Controllers/`, namespace `IdentityService.API.Controllers`. Никаких контроллеров в корне проекта.
- **Request/Response DTO** — только `sealed record` с `required`-свойствами `{ get; init; }`, не класс и не позиционный record. DTO — неизменяемый слепок данных на один проход, а не объект с поведением; `required init` убирает предупреждения компилятора о nullable-свойствах без значения и не даёт случайно создать DTO с недозаполненными полями.
- **Маппинг** — только через **Mapster** (`IMapper`/`MapsterMapper`, внедряется в конструктор контроллера), в контроллере не должно быть ручного `new User { ... }` или `new SomethingResponse { ... }`. Все конфигурации маппинга — в `Mapping/MappingRegister.cs` (класс, реализующий `IRegister`), который подхватывается автоматически через `builder.Services.AddMapster()` в `Program.cs` (Mapster сканирует сборку на реализации `IRegister`). Для DTO/сущностей, где имена свойств совпадают, отдельная конфигурация в `MappingRegister` не обязательна для работы маппинга, но запись `config.NewConfig<TSource, TDestination>()` всё равно добавляется явно — так все пары маппинга видны в одном месте и легко расширяются, если понадобится кастомная логика.
- **Валидация** — только через **FluentValidation**. На каждый request-DTO — свой `AbstractValidator<TRequest>` в `Validators/`. Ничего вызывать вручную в контроллере не нужно: `ValidationFilter` (`Filters/ValidationFilter.cs`, зарегистрирован глобально через `options.Filters.AddValidationFilter()` — extension-метод на `FilterCollection`, объявлен в том же файле) проверяет каждый аргумент действия, для типа которого в DI зарегистрирован `IValidator<T>`, и при ошибке сразу возвращает `400` с `ValidationProblemDetails` — до входа в тело метода контроллера. Валидаторы регистрируются автоматически через `AddValidatorsFromAssemblyContaining<Program>()`.

Регистрация контроллеров/JSON-опций/`ValidationFilter`/OpenAPI/FluentValidation/Mapster собрана в один вызов `builder.Services.AddApiServices()` (`Configuration/ServicesConfiguration.cs`), а не расписана инлайном в `Program.cs`.

Практическое следствие: чтобы добавить новый эндпоинт с телом запроса, обычно не нужно писать ничего вручную ни для маппинга, ни для валидации — только: DTO, `AbstractValidator` на 3–5 строк, запись в `MappingRegister`, и сам метод контроллера, который просто вызывает `mapper.Map<...>()`. Актуальный скилл для этого — `.claude/skills/add-identity-endpoint/SKILL.md`.

### Keycloak как провайдер идентификации

Аутентификация делегирована **Keycloak**, а не выпускается сервисом самостоятельно. Сервис `keycloak` в `docker-compose.yml` импортирует `docker/keycloak/realm-export.json` при каждом старте (`start-dev --import-realm`) — этот файл является источником истины для realm (`cargoservice`), его 5 realm-ролей (соответствуют `IdentityService.Domain.Enums.Role`) и confidential-клиента `identity-service` (service account с ролями `manage-users` и `view-realm` на `realm-management` — вторая нужна, чтобы сервис мог прочитать realm-роль перед назначением её пользователю; без неё регистрация падает `500` с `403` от Keycloak на чтение роли, `directAccessGrantsEnabled` — для прокси-логина через ROPC, см. `AuthController`). Правки realm/клиента вносятся в этот JSON, а не через админ-консоль — изменения из консоли не переживают пересоздание контейнера, а уже импортированный realm при рестарте контейнера **не** переимпортируется повторно (Keycloak по умолчанию пропускает импорт, если realm уже существует) — правки в `realm-export.json` требуют пересоздания тома `postgres_data` (или ручного применения через Admin API/консоль) чтобы попасть в уже поднятый стек.

- **Валидация токена** (`AddKeycloakAuthentication()` в `Configuration/AuthenticationConfiguration.cs`, вызывается из `Program.cs`): настройки читаются в `KeycloakAuthOptions` (`Configuration/KeycloakAuthOptions.cs`, `KeycloakAuthOptions.Bind(configuration)`) — `BaseUrl`/`Realm`/`ClientId`/`ValidIssuer` из секции `Keycloak`. `AddJwtBearer` строит адрес OIDC-метаданных из `BaseUrl`/`Realm` и валидирует токен против `ValidIssuer`. Эти два значения намеренно разделены: `BaseUrl` — это любой URL, по которому реально можно достучаться до Keycloak по HTTP (`http://localhost:8080` локально, переопределяется на `http://keycloak:8080` через переменную окружения `Keycloak__BaseUrl`, когда оба сервиса работают в `docker-compose`), тогда как `ValidIssuer` всегда равен `http://localhost:8080/realms/cargoservice`, потому что именно это значение `KC_HOSTNAME: localhost` (в `docker-compose.yml`) прописывает в claim `iss` каждого токена независимо от того, как до Keycloak достучались. Если когда-нибудь измените `KC_HOSTNAME`, `ValidIssuer` нужно менять вместе с ним.
- **`KeycloakBackchannelHandler`** (там же, `AuthenticationConfiguration.cs`): discovery-документ Keycloak тоже привязан к `KC_HOSTNAME` и указывает `jwks_uri` на `http://localhost:8080/...` — недостижимый изнутри контейнера identity-service (там `localhost` — это сам identity-service, запрос за ключами подписи буквально улетает в его же роутинг и получает `404`). Без этого хендлера **любой** валидный токен отклоняется с `401 invalid_token: "The signature key was not found"` при запуске через `docker compose`, хотя при локальном `dotnet run` та же логика работает случайно правильно (там `localhost` — это и есть Keycloak). Хендлер переписывает хост у всех backchannel-запросов (и discovery, и JWKS) на заведомо достижимый `BaseUrl`, независимо от `KC_HOSTNAME`.
- **Claim-ы ролей**: Keycloak кладёт realm-роли в JSON-claim `realm_access.roles`, а не в отдельные claim-ы. Обработчик `OnTokenValidated` (там же, `MapKeycloakRolesToRoleClaims`) копирует те роли, что совпадают с enum `Role`, в `ClaimTypes.Role`, чтобы `[Authorize(Roles = ...)]` работал как обычно.
- **Провижининг пользователей**: `IIdentityProviderClient` (интерфейс в `IdentityService.Application`, реализация `KeycloakIdentityProviderClient` в `IdentityService.Infrastructure/Keycloak/`) создаёт пользователя в Keycloak через Admin REST API (client-credentials grant с использованием service account) и назначает realm-роль. `UsersService.CreateUserAsync` вызывает его *до* записи локальной строки, используя id, сгенерированный Keycloak, как `User.Id` — так что `User.Id` в identity-service и id пользователя в Keycloak всегда совпадают (это один и тот же GUID). Отката нет, если локальная запись в БД падает после успешного вызова Keycloak (пользователь-сирота в Keycloak) — приемлемо на данном этапе, вернуться к этому, если станет реальной проблемой.
- `POST api/users/register` остаётся `[AllowAnonymous]` и всегда создаёт `Client`; `POST api/users/staff` (только `Admin`) — способ создать аккаунт не-клиента — оба идут через один и тот же путь провижининга в Keycloak.

`AuthController` (`[AllowAnonymous]`) проксирует запросы к token endpoint Keycloak через `IIdentityProviderClient`, оба метода возвращают `{ accessToken, refreshToken, expiresIn, tokenType }` либо `401`: `POST api/auth/login` (`AuthenticateAsync`, ROPC grant) и `POST api/auth/refresh` (`RefreshAsync`, `grant_type=refresh_token`). Для ручного тестирования подходит засеянный пользователь `admin@cargoservice.local` / `admin` из `realm-export.json` — против `login`.

### Публикация событий: транзакционный outbox

`UsersService.CreateUserAsync` публикует `CargoService.Contracts.Events.V1.UserRegistered` — но только когда `user.Role == Role.Client` (самостоятельная регистрация), не для аккаунтов, созданных через `api/users/staff`, поскольку событие существует для того, чтобы clients-service автоматически создавал `ClientAccount`, а сотрудники не являются клиентами.

Путь записи разделён на два порта Application, оба реализованы в `IdentityService.Persistence/Outbox/` поверх одного и того же `AppDbContext`:
- `IOutboxWriter.Enqueue(...)` — вызывается `UsersService` *до* `usersRepository.CreateAsync(user, ...)`. Он только ставит строку `OutboxMessage` в очередь на уровне DbContext (без `SaveChanges`); следующий сразу за ним вызов репозитория делает реальный `SaveChangesAsync`, который — поскольку оба идут через один и тот же scoped-экземпляр `AppDbContext` — коммитит вставку `User` и вставку `OutboxMessage` в одной транзакции БД. Этот порядок (сначала enqueue, затем позволить следующему вызову репозитория сохранить) — вся суть трюка, и его легко случайно сломать; компилятор здесь unit-of-work не защищает.
- `IOutboxReader` (`GetPendingAsync`/`MarkProcessedAsync`) — используется диспетчером ниже, а не кодом обработки запросов.

`IdentityService.Infrastructure/Outbox/OutboxDispatcher.cs` — это `BackgroundService` (регистрируется через `AddHostedService` в `AddInfrastructure`), который опрашивает `IOutboxReader` каждые 5с, публикует накопленные строки в `RabbitMqConventions.EventsExchange` (topic-exchange `cargoservice.events`), используя предвычисленный routing key каждой строки (`RabbitMqConventions.RoutingKey("identity-service", nameof(UserRegistered))` → `identity-service.user-registered`), и помечает их обработанными. Если RabbitMQ недоступен или соединение обрывается, весь цикл подключения+опроса повторяется через 10с вместо падения хоста — необработанное исключение в `BackgroundService` иначе по умолчанию роняет всё приложение. Настройки подключения к RabbitMQ берутся из секции конфигурации `RabbitMQ` / переопределения переменной окружения `RabbitMQ__HostName` в `docker-compose.yml` (тот же паттерн переопределения, что у `ConnectionStrings`/`Seq`/`Keycloak`).

Сейчас никто не потребляет `identity-service.user-registered` — это должен делать clients-service, который ещё не построен (см. spec.md, Фаза 2).

## Команды

Сборка/запуск identity-service (из `services/identity-service/`):
```
dotnet build IdentityService.slnx
dotnet run --project IdentityService
```

Полный локальный стек (Postgres + RabbitMQ + MinIO + Seq + Keycloak + API identity-service), из корня репозитория:
```
docker compose up --build
```
`docker-compose.override.yml` подхватывается автоматически (без `-f`) и запускает сервис через dev-стадию Dockerfile (`dotnet watch`, исходники смонтированы как volume) вместо опубликованного `final`-образа — правки в `.cs`-файлах под `services/identity-service/` (или `shared/CargoService.Contracts/`) подхватываются в контейнере без пересборки. dev-стадия/override-запись для каждого сервиса добавляется вместе с его `Dockerfile` по мере реализации.

Контекст сборки `services/identity-service/Dockerfile` — **корень репозитория** (`build.context: .` в `docker-compose.yml`, `dockerfile: services/identity-service/Dockerfile`), а не сам `services/identity-service/` — `IdentityService.Application` ссылается на `shared/CargoService.Contracts`, которая лежит вне папки сервиса и иначе была бы недостижима для Docker. Та же причина у bind-mount dev-стадии в `docker-compose.override.yml`: он монтирует весь корень репозитория (`.:/src`), а не только `services/identity-service/`, чтобы `shared/CargoService.Contracts` тоже была видна для `dotnet watch`. Если добавите сервис, чей Dockerfile нуждается только в собственной папке (без зависимости на shared-проект), более узкий контекст на сервис проще и вполне подходит — паттерн «весь корень репозитория» нужен только из-за зависимости на Contracts.

EF Core-миграции (запускаются из `services/identity-service/IdentityService.Persistence/`):
```
dotnet ef migrations add <Name>
dotnet ef database update
```
`--startup-project` не нужен — `AppDbContextFactory` (`IDesignTimeDbContextFactory<AppDbContext>`) позволяет `dotnet ef` строить `AppDbContext` напрямую вместо сборки всего хоста `IdentityService`, который иначе также запускал бы (и требовал бы валидной конфигурации для) `AddKeycloakAuthentication`/`AddInfrastructure` (Keycloak/RabbitMQ). Фабрика читает строку подключения из переменной окружения `ConnectionStrings__DefaultConnection`, откатываясь к тому же локальному дефолту Postgres, что и `appsettings.json`. `AddPersistence` (регистрация времени выполнения) и фабрика — оба включают `EnableRetryOnFailure()` на провайдере Npgsql.

## Тесты

`services/identity-service/IdentityService.Application.Tests/` — unit-тесты на xUnit + Moq для `UsersService` (мокает `IUsersRepository`/`IIdentityProviderClient`/`IOutboxWriter`; покрывает присвоение id от провайдера идентификации, правило постановки в outbox только для роли Client, делегирование update/delete).

`services/identity-service/IdentityService.IntegrationTests/` — xUnit + `Microsoft.AspNetCore.Mvc.Testing` + Testcontainers, гоняет реальный API поверх одноразового контейнера Postgres (`IdentityApiFactory`, один контейнер на тестовый класс через `IClassFixture`). Два заменителя подставляются вместо того, что нужно реальному деплою, но эти тесты не разворачивают: `FakeIdentityProviderClient` (без реального Keycloak) и `TestAuthHandler` (читает заголовок запроса `X-Test-Roles` через запятую вместо валидации настоящего JWT; становится дефолтной auth-схемой через `ConfigureTestServices`, переопределяя `AddJwtBearer` из `AddKeycloakAuthentication()`). `IHostedService` диспетчера outbox в тестах тоже убран — контейнер RabbitMQ не нужен. Тесты проверяют HTTP-статус-коды и, через второй `AppDbContext`, указывающий на тот же контейнер, напрямую строки `Users`/`OutboxMessages` (например, регистрация `Client` создаёт и строку пользователя, и ровно одну строку outbox с routing key `identity-service.user-registered`; создание сотрудника — нет).

```
dotnet test IdentityService.Application.Tests   # без внешних зависимостей
dotnet test IdentityService.IntegrationTests    # нужен запущенный Docker-демон (Testcontainers)
```
