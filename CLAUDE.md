# CLAUDE.md

Этот файл содержит инструкции для Claude Code (claude.ai/code) при работе с кодом в этом репозитории.

## Обзор проекта

Платформа грузоперевозок — **монорепозиторий микросервисов на .NET 10 / C#**: PostgreSQL на каждый сервис, RabbitMQ для межсервисных событий (транзакционный outbox у издателей, inbox-дедупликация у части потребителей), Keycloak как провайдер идентификации, Serilog → Seq для централизованного логирования.

Полная целевая архитектура, сущности/API/события каждого сервиса и бэклог по фазам — в [spec.md](spec.md); это источник истины о том, что строить дальше. Читайте его перед планированием кросс-сервисной работы и держите его чек-листы актуальными по мере выполнения задач.

Реализованы **все десять сервисов** под `services/` (фазы 0–9 и 11 бэклога). Не сделаны: Logistics Service (Фаза 10), API Gateway (Фаза 12), наблюдаемость/DevOps — health checks, метрики, трассировка, CI (Фаза 13), сквозные end-to-end тесты (Фаза 14).

## Структура репозитория

```
services/{service-name}/         один микросервис на папку
shared/CargoService.Contracts/   общая библиотека DTO событий (Events/V1/) и конвенций топологии RabbitMQ (Messaging/RabbitMqConventions.cs)
docker/init-db.sql               создание БД и пользователей Postgres под каждый сервис (по одной БД на сервис)
docker/keycloak/realm-export.json  realm как код: роли, клиенты, засеянный админ
spec.md                          архитектурная спецификация + бэклог по фазам
README.md                        описание проекта для GitHub: схема событий, порты, быстрый старт
Directory.Build.props            общие MSBuild-свойства (TargetFramework, Nullable, ImplicitUsings, LangVersion)
Directory.Packages.props         централизованное управление версиями пакетов; csproj ссылаются на пакеты без атрибута Version
.editorconfig                    общие правила стиля/форматирования/именования C#
```

Репозиторного решения на весь монорепо нет — у каждого сервиса свой `{ServiceName}.slnx` внутри его папки.

Соответствие имён (легаси общего названия платформы): папка `services/cargo-service/` — это **cargo-service** (грузы, `CargoService.*`, БД `cargoshipments`, compose-сервис `cargoshipments`, порт 8085), а compose-сервис `cargoservice` с БД `cargoservice` — это **identity-service** (порт 8081). При правках `docker-compose.yml`/`init-db.sql` легко перепутать.

## Общие конвенции всех сервисов

Все сервисы построены по одному шаблону. Отклонения от него перечислены в разделе «Сервисы» ниже; во всём остальном новый код должен повторять этот шаблон, а не изобретать свой.

### Слои и проекты

Clean Architecture, один проект на слой:

- `{ServiceName}.Domain` — только сущности и enum-ы, без зависимостей.
- `{ServiceName}.Application` — сервисы сценариев использования, интерфейсы портов (`I...Repository`, `I...Client`, `IOutboxWriter`, `IEventHandler<T>`), модели/DTO слоя Application. Зависит от Domain и от `CargoService.Contracts`.
- `{ServiceName}.Persistence` — EF Core `DbContext`, `IEntityTypeConfiguration<T>` (подхватываются через `ApplyConfigurationsFromAssembly`), репозитории, миграции, реализации outbox/inbox.
- `{ServiceName}.Infrastructure` — RabbitMQ (диспетчер outbox и консьюмеры), HTTP-клиенты к другим сервисам, фоновые воркеры, внешние интеграции.
- `{ServiceName}` — папка API/хоста ASP.NET Core. Внимание: файл проекта внутри неё называется **`{ServiceName}.API.csproj`** (папка без суффикса, csproj с суффиксом).

### Program.cs и Configuration/

`Program.cs` — короткий манифест вызовов; вся логика вынесена в extension-методы под `Configuration/` соответствующего проекта:

- `AddApplicationServices()` — `{ServiceName}.Application/Configuration/ServicesConfiguration.cs`.
- `AddPersistence(connectionString)` — `{ServiceName}.Persistence/Configuration/PersistenceConfiguration.cs`, регистрирует `DbContext` (Npgsql, с `EnableRetryOnFailure()`) и репозитории.
- `AddInfrastructure(configuration)` — `{ServiceName}.Infrastructure/Configuration/ServicesConfiguration.cs`, регистрирует RabbitMQ-консьюмеры, `OutboxDispatcher`, HTTP-клиенты, фоновые воркеры.
- В API-проекте (`{ServiceName}/Configuration/`) — `AddSerilogLogging()` (`LoggingConfiguration.cs`), `AddKeycloakAuthentication(configuration)` (`AuthenticationConfiguration.cs`), `AddApiServices()` (`ServicesConfiguration.cs`), `MigrateDatabaseAsync()` и `MapDevelopmentEndpoints()` (`WebApplicationConfiguration.cs`).

БД мигрируется при старте (`await app.MigrateDatabaseAsync()` до маппинга контроллеров) — отдельного шага миграции в dev/docker-процессе нет.

OpenAPI и UI документации (**Scalar**, `MapScalarApiReference()`, адрес `/scalar`) поднимаются только в Development, внутри `MapDevelopmentEndpoints()`. Swagger UI в проекте не используется.

### API-слой: контроллеры, маппинг, валидация

Обязательно для любого нового эндпоинта в любом сервисе:

- **Контроллеры** — только в `Controllers/`, namespace `{ServiceName}.API.Controllers`. Никаких контроллеров в корне проекта.
- **Request/Response DTO** — только `sealed record` с `required`-свойствами `{ get; init; }`, не класс и не позиционный record. DTO — неизменяемый слепок данных на один проход; `required init` убирает предупреждения о nullable-свойствах без значения и не даёт создать DTO с недозаполненными полями.
- **Маппинг** — только через **Mapster** (`IMapper`/`MapsterMapper`, внедряется в конструктор контроллера); ручных `new SomethingResponse { ... }` в контроллере быть не должно. Конфигурации — в `Mapping/MappingRegister.cs` (реализация `IRegister`, подхватывается через `AddMapster()`). Для пар с совпадающими именами свойств отдельная конфигурация не обязательна для работы, но `config.NewConfig<TSource, TDestination>()` всё равно пишется явно — чтобы все пары были видны в одном месте.
- **Валидация** — только через **FluentValidation**: на каждый request-DTO свой `AbstractValidator<TRequest>` в `Validators/`. Вручную в контроллере ничего вызывать не нужно: `ValidationFilter` (`Filters/ValidationFilter.cs`, регистрируется глобально через `options.Filters.AddValidationFilter()` — extension-метод на `FilterCollection` в том же файле) проверяет каждый аргумент действия, для типа которого зарегистрирован `IValidator<T>`, и при ошибке возвращает `400` с `ValidationProblemDetails` до входа в тело метода. Валидаторы регистрируются через `AddValidatorsFromAssemblyContaining<Program>()`.

Регистрация контроллеров, JSON-опций, `ValidationFilter`, OpenAPI, FluentValidation и Mapster собрана в один `builder.Services.AddApiServices()`.

Практическое следствие: новый эндпоинт с телом запроса — это DTO, валидатор на 3–5 строк, запись в `MappingRegister` и метод контроллера. Скилл для identity-service: `.claude/skills/add-identity-endpoint/SKILL.md` (его шаги переносятся на любой другой сервис — конвенции те же).

### Конфигурация

Секции `appsettings.json` (`ConnectionStrings`, `Serilog`/`MinimumLevel`, `Seq`, `Keycloak`, `RabbitMQ`, плюс сервис-специфичные — `FileStorageService`, `Smtp`, `Onnx`, `Payments` и т.п.) переопределяются в `docker-compose.yml` переменными окружения через двойное подчёркивание: `ConnectionStrings__DefaultConnection`, `Seq__ServerUrl`, `Keycloak__BaseUrl`, `RabbitMQ__HostName`. Это общий паттерн — в контейнере меняются только хосты, структура конфигурации одна и та же.

Логирование: Serilog подключается через `AddSerilogLogging()` на `WebApplicationBuilder`, а не через стандартную секцию `Logging`. Логи всегда идут в консоль; дополнительно в Seq, если задан `Seq:ServerUrl` (`http://localhost:5341` локально, `http://seq:80` в compose).

### Аутентификация: Keycloak

Аутентификация делегирована **Keycloak**; сервисы токены не выпускают. Контейнер `keycloak` импортирует `docker/keycloak/realm-export.json` при старте (`start-dev --import-realm`) — этот файл источник истины для realm `cargoservice`, его 5 realm-ролей (соответствуют `IdentityService.Domain.Enums.Role`) и клиентов (включая confidential-клиент `identity-service` с service account: роли `manage-users` и `view-realm` на `realm-management` — вторая нужна, чтобы прочитать realm-роль перед назначением, без неё регистрация падает `500` с `403` от Keycloak; `directAccessGrantsEnabled` — для прокси-логина через ROPC).

Правки realm вносятся в этот JSON, а не через админ-консоль: изменения из консоли не переживают пересоздание контейнера, а уже импортированный realm при рестарте **не** переимпортируется (Keycloak пропускает импорт, если realm существует). Чтобы правка доехала до поднятого стека, нужно пересоздать том `postgres_data` или применить изменение вручную через Admin API/консоль.

В каждом API-проекте:

- **Валидация токена** — `AddKeycloakAuthentication()` в `Configuration/AuthenticationConfiguration.cs`; настройки читаются в `KeycloakAuthOptions` (`BaseUrl`/`Realm`/`ClientId`/`ValidIssuer`, секция `Keycloak`). `BaseUrl` и `ValidIssuer` намеренно разделены: `BaseUrl` — адрес, по которому Keycloak реально достижим (`http://localhost:8080` локально, `http://keycloak:8080` в compose), а `ValidIssuer` всегда `http://localhost:8080/realms/cargoservice`, потому что именно это значение `KC_HOSTNAME: localhost` прописывает в claim `iss` любого токена. Меняете `KC_HOSTNAME` — меняйте `ValidIssuer` вместе с ним.
- **`KeycloakBackchannelHandler`** (там же) переписывает хост у backchannel-запросов (discovery и JWKS) на `BaseUrl`. Без него discovery-документ уводит сервис за ключами подписи на `http://localhost:8080/...`, что внутри контейнера означает его собственный роутинг и `404`, и **любой** валидный токен отклоняется с `401 invalid_token: "The signature key was not found"` — при этом при локальном `dotnet run` та же логика работает случайно правильно.
- **Claim-ы ролей**: Keycloak кладёт realm-роли в JSON-claim `realm_access.roles`. Обработчик `OnTokenValidated` (`MapKeycloakRolesToRoleClaims`) копирует известные роли в `ClaimTypes.Role`, чтобы `[Authorize(Roles = ...)]` работал обычным образом.

### Межсервисные HTTP-вызовы

Синхронные вызовы используются точечно: orders → pricing (расчёт цены) и cargo/document/ai-inspection → file-storage (presigned URL).

Клиент живёт в `{ServiceName}.Infrastructure/{Target}/`, за интерфейсом из Application (`IPricingClient`, `IFileStorageClient`), регистрируется через `AddHttpClient<>` с политиками **Polly** (retry + таймаут на попытку; общий таймаут `HttpClient` — предохранитель, он специально больше, чем таймаут попытки, иначе обрезал бы retry на середине).

Авторизация машина-к-машине — `ServiceTokenProvider` (`Infrastructure/Keycloak/`): client credentials grant по `KeycloakServiceAccount`-секции, токен кэшируется на процесс (поэтому провайдер — singleton).

### Событийный обмен

Общие контракты — `shared/CargoService.Contracts`: DTO событий в `Events/V1/` (все наследуют `IntegrationEvent` с `EventId`), конвенции в `Messaging/RabbitMqConventions.cs`:

- один topic exchange `cargoservice.events` (`EventsExchange`);
- routing key — `{publishing-service}.{event-name-in-kebab-case}` (`RoutingKey("identity-service", nameof(UserRegistered))` → `identity-service.user-registered`);
- имя очереди — `{consuming-service}.{event-name-in-kebab-case}`, DLQ — то же имя с суффиксом `.dlq`.

**Публикация — транзакционный outbox.** Два порта Application поверх одного `AppDbContext`:

- `IOutboxWriter.Enqueue(...)` вызывается сценарием *до* вызова репозитория. Он только добавляет строку `OutboxMessage` в DbContext (без `SaveChanges`); следующий вызов репозитория делает `SaveChangesAsync`, который — поскольку это тот же scoped-экземпляр `AppDbContext` — коммитит доменную строку и строку outbox одной транзакцией. Этот порядок и есть суть паттерна, сломать его легко: компилятор unit-of-work здесь не защищает.
- `IOutboxReader` (`GetPendingAsync`/`MarkProcessedAsync`) используется только диспетчером.

`{ServiceName}.Infrastructure/Outbox/OutboxDispatcher.cs` — `BackgroundService`: опрашивает reader каждые 5с, публикует строки в exchange по их предвычисленному routing key, помечает обработанными. При недоступности RabbitMQ цикл подключения повторяется через 10с, а не роняет хост (необработанное исключение в `BackgroundService` по умолчанию завершает приложение).

Outbox есть у: identity, pricing, orders, cargo, ai-inspection, document, payment.

**Потребление.** Два стиля, оба — `BackgroundService` с собственной очередью, DLQ (`x-dead-letter-exchange: ""` + `x-dead-letter-routing-key: {queue}.dlq`, дефолтный exchange роутит по имени очереди — отдельный DLX не нужен), `prefetchCount: 10`, ручной ack и переподключением через 10с:

- **Обобщённый `EventConsumer<TEvent>`** (notification, document, payment) — одна реализация на все подписки: десериализует payload, проверяет inbox, вызывает `IEventHandler<TEvent>` из скоупа, отмечает обработку. Подписки объявляются строками `services.AddEventConsumer<OrderConfirmed>("orders-service")`.
- **Отдельный консьюмер на событие** (clients `UserRegisteredConsumer`, orders, cargo, ai-inspection) — класс на подписку.

**Inbox-дедупликация** (`IInboxRepository` + `Persistence/Inbox/`) есть у notification, document, payment, ai-inspection. Отметка ставится **после** обработки, а не до: при падении между отметкой и записью худший случай — повторная обработка (от неё защищают доменные инварианты), тогда как обратный порядок потерял бы событие навсегда. У clients, orders и cargo inbox-а нет — идемпотентность там обеспечивается доменными проверками.

В DLQ уходит только то, что действительно не обработать: битый payload, отказ БД.

### Тесты

У каждого сервиса, кроме file-storage-service, два тестовых проекта:

- `{ServiceName}.Application.Tests` — xUnit + Moq на сервисы слоя Application (репозитории, клиенты и `IOutboxWriter` мокаются). Внешних зависимостей не требуют.
- `{ServiceName}.IntegrationTests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing` + Testcontainers: реальный API поверх одноразового контейнера Postgres (`{Service}ApiFactory`, один контейнер на класс через `IClassFixture`). Нужен запущенный Docker.

Что подменяется в интеграционных тестах: `TestAuthHandler` (читает заголовок `X-Test-Roles` через запятую вместо валидации JWT; становится дефолтной схемой через `ConfigureTestServices`, переопределяя `AddJwtBearer`), фейковые HTTP-клиенты (`FakeIdentityProviderClient`, `FakePricingClient` и т.п.), и `services.RemoveAll<IHostedService>()` — чтобы не поднимать RabbitMQ. Проверяются и HTTP-статусы, и, через второй `AppDbContext` на тот же контейнер, строки в БД (включая `OutboxMessages` с ожидаемым routing key).

**Известная проблема.** Строку подключения в тестовой фабрике нужно задавать через `builder.UseSetting("ConnectionStrings:DefaultConnection", ...)`, а не через `ConfigureAppConfiguration`: `Program.cs` читает конфигурацию **до** `builder.Build()`, а источники из `ConfigureAppConfiguration` подмешиваются только на этапе построения хоста, то есть позже. В результате тесты молча уходят на `localhost:5432` из `appsettings.json` и падают с `28P01`. На `UseSetting` переведены фабрики notification, document, payment, ai-inspection; **ещё не переведены** identity, clients, pricing, orders, cargo — их «падающие из-за окружения» интеграционные тесты падают именно поэтому. Второй нюанс того же места: схему создаёт сам хост при старте (`MigrateDatabaseAsync`), а `WebApplicationFactory` поднимает его лениво — если тест сначала засеивает данные через `DbContext`, в `InitializeAsync` после старта контейнера нужно дёрнуть `CreateClient()`, иначе будет `42P01: relation ... does not exist`.

### Docker

Контекст сборки каждого Dockerfile — **корень репозитория** (`build.context: .`, `dockerfile: services/{service}/Dockerfile`), потому что `{ServiceName}.Application` ссылается на `shared/CargoService.Contracts` вне папки сервиса. По той же причине dev-стадия в `docker-compose.override.yml` монтирует весь корень (`.:/src`).

`docker-compose.override.yml` подхватывается автоматически (без `-f`) и запускает сервисы через dev-стадию Dockerfile (`dotnet watch`, исходники смонтированы как volume) вместо опубликованного `final`-образа — правки в `.cs` подхватываются без пересборки образа.

## Сервисы

Порты, назначение и адреса инфраструктурных UI — в [README.md](README.md). Ниже — то, что специфично для работы с кодом каждого сервиса.

**identity-service** (8081, БД `cargoservice`, compose-сервис `cargoservice`). `User` без поля пароля — учётные данные целиком в Keycloak. `IIdentityProviderClient`/`KeycloakIdentityProviderClient` создаёт пользователя через Admin REST API и назначает realm-роль; `UsersService.CreateUserAsync` вызывает его *до* записи локальной строки и использует сгенерированный Keycloak id как `User.Id` — это один и тот же GUID. Отката нет, если локальная вставка упадёт после успешного вызова Keycloak (сирота в Keycloak) — принято осознанно. `POST api/users/register` — `[AllowAnonymous]`, всегда создаёт `Client`; `POST api/users/staff` (только `Admin`) — остальные роли. `UserRegistered` публикуется только для роли `Client`: событие существует ради создания `ClientAccount` в clients-service, а сотрудники клиентами не являются. `AuthController` (`[AllowAnonymous]`) проксирует token endpoint Keycloak: `POST api/auth/login` (ROPC) и `POST api/auth/refresh`, оба возвращают `{ accessToken, refreshToken, expiresIn, tokenType }` либо `401`. Для ручной проверки есть засеянный `admin@cargoservice.local` / `admin`.

**clients-service** (8082). `Counterparty` и `ClientAccount`, CRUD и поиск контрагентов. `UserRegisteredConsumer` создаёт `ClientAccount` по событию identity-service. Своих событий не публикует — outbox-а нет.

**pricing-service** (8083). `TariffRate` (категории `ShippingType`/`PackagingType`/`PickupDelivery`/`Insurance`, коды в `TariffCodes`), `TariffsController` (чтение всем, правка админом) и `PricingController` с публичным расчётом. Публикует `TariffChanged` через outbox. Консьюмеров нет.

**orders-service** (8084). `Order` с вложенными `OrderParty`/`OrderServiceOptions`. При создании заявки синхронно вызывает pricing (`IPricingClient` + `InMemoryCalculationCache`, TTL 10 минут); кэш целиком сбрасывается по `TariffChanged` (`IMemoryCache` не умеет «очистить всё» — все записи привязаны к общему `CancellationTokenSource`, `Clear()` отменяет его и ставит новый). Публикует `OrderCreated`/`OrderConfirmed`/`OrderCancelled`, потребляет `CargoStatusChanged`, `PaymentCompleted`, `TariffChanged`. Доступ к заявкам — только своим: владелец берётся из claim `sub` токена (`GetUserId()` в контроллере), а не из query-параметра.

**cargo-service** (8085, БД `cargoshipments`). `Shipment`, `AcceptanceInspection`, `PackagingService`, `ShipmentStatusHistory`. Создаёт груз по `OrderConfirmed`, потребляет `PackageIntegrityAssessed`, публикует `CargoAccepted`/`CargoStatusChanged`/`CargoPhotoUploaded`/`CargoDelivered`. `TrackingController` — публичный трекинг без авторизации. `SlaMonitor` — фоновая джоба: раз в 5 минут пачками по 100 переводит грузы с истёкшим `DeliveryDeadline` в «Задерживается» обычным путём смены статуса (с историей и событием). Фото складывает в file-storage через `IFileStorageClient`.

**file-storage-service** (8086). Единственный сервис **без БД**: нет Domain и Persistence, нет миграций и тестов. `MinioFileStorage` поверх MinIO, `BucketInitializer` создаёт бакет при старте, `FilesController` выдаёт presigned URL на загрузку и скачивание. `MinioClients` держит два клиента к одному хранилищу: `Operations` ходит по внутреннему адресу (`minio:9000`), `Presigning` подписывает ссылки внешним (`localhost:9000`). Переписать хост в готовой ссылке нельзя — он входит в подпись SigV4, и MinIO ответит `403`.

**notification-service** (8087). Самый «слушающий» сервис: 11 подписок через обобщённый `EventConsumer<TEvent>` и по `IEventHandler<T>` на каждое событие. `TemplateRenderer` подставляет значения в `NotificationTemplate`, отправка — через `INotificationSenderRegistry` по каналам: `SmtpEmailSender` (в dev — Mailpit), `TwilioSmsSender`/`LoggingSmsSender`. Ведёт `NotificationLog`, `NotificationRecipient`, `NotificationPreference`; `NotificationsController` отдаёт историю клиенту. Есть inbox.

**ai-inspection-service** (8088). Потребляет `CargoPhotoUploaded`, публикует `PackageIntegrityAssessed`. `IPackageInspectionModel` реализуется `OnnxPackageInspectionModel` (singleton — `InferenceSession` держит веса в памяти) **или** `StubPackageInspectionModel`, если файл модели не найден: отсутствие модели — нормальное состояние dev-стенда и тестов, заглушка пишет предупреждение на каждый вердикт. Порог `DamageConfidenceThreshold` — бизнес-правило, живёт в `InspectionOptions` в Application. `InspectionWorker` обрабатывает очередь `InspectionJob`, `ModelEvaluator` считает метрики по размеченному набору.

**document-service** (8089). Потребляет `OrderCreated`/`OrderConfirmed` (складывает `OrderSnapshot` — данные заявки нужны позже, ходить за ними в orders он не может) и `CargoAccepted`/`CargoDelivered` (генерирует накладную и акты). Рендер — QuestPDF (`QuestPdfDocumentRenderer`, шрифты PT Sans лежат в `Infrastructure/Fonts/`), трек-код — `QrTrackingCodeGenerator`. Готовый PDF кладётся в file-storage, публикуется `DocumentGenerated`.

**payment-service** (8090). Выставляет счёт по `OrderConfirmed`, возвращает деньги по `OrderCancelled`, публикует `PaymentCompleted`/`PaymentFailed`/`RefundIssued`. `IPaymentProviderClient` — `YooKassaPaymentProviderClient` при настроенных кредах, иначе `SandboxPaymentProviderClient`. `PaymentsController` принимает webhook провайдера; исход платежа обрабатывает `PaymentWebhooksService`. Счёт уникален по заявке — это и есть защита от повторной обработки события.

## Команды

Сборка и запуск одного сервиса (из его папки, например `services/identity-service/`):

```
dotnet build IdentityService.slnx
dotnet run --project IdentityService
```

Полный локальный стек (Postgres, RabbitMQ, MinIO, Mailpit, Seq, Keycloak и все десять сервисов), из корня репозитория:

```
docker compose up --build
```

EF Core-миграции — из папки `{ServiceName}.Persistence` соответствующего сервиса:

```
dotnet ef migrations add <Name>
dotnet ef database update
```

`--startup-project` не нужен: `AppDbContextFactory` (`IDesignTimeDbContextFactory<AppDbContext>`) позволяет `dotnet ef` построить `AppDbContext` напрямую, не собирая хост API (который иначе потребовал бы валидной конфигурации Keycloak/RabbitMQ). Фабрика читает `ConnectionStrings__DefaultConnection` из переменных окружения, откатываясь к локальному дефолту из `appsettings.json`.

Тесты:

```
dotnet test services/identity-service/IdentityService.Application.Tests   # без внешних зависимостей
dotnet test services/identity-service/IdentityService.IntegrationTests    # нужен запущенный Docker (Testcontainers)
```
