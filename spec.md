# Спецификация системы «Сервис грузоперевозок»

## 1. Общая идея

Микросервисная система на **.NET 10 / C#**, описывающая полный жизненный цикл груза: заявка → приёмка → перевозка → выдача. Каждый микросервис — отдельный проект **Clean Architecture** с явным разделением на слои:

```
{ServiceName}/
├── {ServiceName}.Domain/          # сущности, value objects, доменные события, интерфейсы репозиториев
├── {ServiceName}.Application/     # use-cases/сервисы, DTO, интерфейсы, валидация, маппинг
├── {ServiceName}.Persistence/     # EF Core + PostgreSQL, репозитории, миграции
├── {ServiceName}.Infrastructure/  # RabbitMQ, внешние интеграции (S3, ИИ, SMTP/SMS), фоновые джобы
└── {ServiceName}.API/             # ASP.NET Core Web API, контроллеры/эндпоинты, DI, Program.cs
```

Первая реализация (`IdentityService.Domain/Application/Persistence` + `IdentityService` API, сущность `User`) перенесена в `services/identity-service/` и становится основой сервиса **Identity** (см. Фазу 0).

### Общие технические решения
- **БД**: PostgreSQL, схема "database per service" — у каждого сервиса своя БД/схема, прямой доступ к чужой БД запрещён.
- **Межсервисная коммуникация**:
  - Асинхронная (основная) — **RabbitMQ**, паттерн publish/subscribe, обмен `topic`, per-service очереди с DLQ (dead-letter queue) и retry.
  - Синхронная (когда нужен немедленный ответ, напр. расчёт цены при создании заявки) — HTTP/gRPC через внутреннюю сеть, вызывается из API Gateway/сервиса-инициатора.
- **API Gateway** — единая точка входа для клиентов (веб/моб. приложение), маршрутизация, агрегация, аутентификация.
- **Identity** — JWT (access + refresh), роли: `Client`, `Manager`, `WarehouseOperator`, `Courier`, `Admin`.
- **Наблюдаемость**: структурированные логи (Serilog) → Seq/ELK, health checks (`/health`), метрики (OpenTelemetry → Prometheus/Grafana), трассировка запросов между сервисами (correlation id / traceparent).
- **Контейнеризация**: Dockerfile на сервис, общий `docker-compose.yml` для локального окружения (Postgres×N, RabbitMQ, Gateway, все сервисы).
- **Идемпотентность и надёжность обмена сообщениями**: Outbox-паттерн в сервисах, публикующих события (транзакционная запись в БД + фоновая отправка), Inbox/дедупликация на потребителях.
- **Тестирование**: unit-тесты Domain/Application (xUnit + Moq/NSubstitute), интеграционные тесты Persistence/API (Testcontainers для Postgres/RabbitMQ), контрактные тесты событий.

---

## 2. Микросервисы

### 2.1 Identity Service — аутентификация и пользователи системы
Уже частично реализован в текущем репозитории.

**Назначение**: регистрация/аутентификация сотрудников и клиентов, роли, выдача JWT.

**Сущности**: `User` (Id, Name, LastName, Phone, Email, PasswordHash, Role, IsDeactivated).

**API**: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `GET /users/{id}`, `GET /users`, `PUT /users/{id}`, `DELETE /users/{id}`.

**События**: публикует `UserRegistered`.

---

### 2.2 Clients Service — контрагенты (отправители/получатели)
**Назначение**: справочник физ./юр. лиц — отправителей и получателей, используемых в заявках; хранение истории контрагентов клиента для повторного использования при создании новой заявки.

**Сущности**:
- `Counterparty` (Id, Type: Физ.лицо/Организация, ОрганизацияНазвание, ФИО, ИНН(опц.), City, Phone, Email).
- `ClientAccount` (Id, UserId → Identity, привязанные Counterparty).

**API**: CRUD по контрагентам, поиск по городу/телефону/названию.

**События**: подписывается на `UserRegistered` (создаёт `ClientAccount`).

---

### 2.3 Pricing Service — тарифы и стоимость услуг
**Назначение**: пункт 3 — хранение фиксированных цен и расчёт итоговой стоимости услуг заявки.

**Сущности**:
- `TariffRate` (Id, Category: ShippingType|PackagingType|PickupDelivery|Insurance, Code, Name, Price, ValidFrom, ValidTo).
  - ShippingType: Обычная / Экспресс.
  - PackagingType: Деревянная / Паллет / Спец. упаковка.
  - PickupDelivery: Забор / Доставка.
  - Insurance: страхование груза (% от заявленной стоимости или фикс. ставка).
- `PriceCalculationRequest`/`PriceCalculationResult` (DTO для расчёта: вес, объём, город отправления/получения, выбранные услуги → итоговая цена с разбивкой).

**API**: `GET /tariffs`, `PUT /tariffs/{id}` (админ), `POST /pricing/calculate` (синхронный расчёт для Orders Service).

**События**: публикует `TariffChanged` (для инвалидации кэша у Orders Service).

---

### 2.4 Orders Service — приём и хранение заявок клиента
**Назначение**: пункт 1 — основной сервис заявок.

**Сущности**:
- `Order` (Id, Number, Status: Draft/Created/Confirmed/Cancelled, CreatedAt).
  - Sender: City, OrganizationOrPersonName, IsOrganization, Phone.
  - Recipient: City, OrganizationOrPersonName, IsOrganization, Phone.
  - OriginCity, DestinationCity.
  - CargoName, CargoWeight, CargoQuantity.
  - DeclaredValue (стоимость груза для страхования/компенсации).
  - RequestedShipDate, DeliveryDeadline.
  - ServiceOptions: ShippingType, PackagingType, NeedsPickup, NeedsDelivery, NeedsInsurance.
  - CalculatedPrice (получен от Pricing Service).
  - ClientAccountId, SenderCounterpartyId, RecipientCounterpartyId.

**API**: `POST /orders`, `GET /orders/{id}`, `GET /orders` (self-service для `Client`, фильтр по `ClientAccountId` из JWT, без query-параметра `clientId` — см. Фазу 4), `PUT /orders/{id}`, `POST /orders/{id}/confirm`, `POST /orders/{id}/cancel`.

**Взаимодействие**: при создании заявки синхронно вызывает `Pricing.calculate`; после подтверждения публикует `OrderCreated`/`OrderConfirmed`.

**События**: публикует `OrderCreated`, `OrderConfirmed`, `OrderCancelled`. Подписывается на `CargoStatusChanged` (для отображения статуса в заявке), `PaymentCompleted`.

---

### 2.5 Cargo Service — приёмка, обработка и трекинг груза
**Назначение**: пункт 2 — процесс приёмки груза сотрудником, оценка состояния, услуги по упаковке, забор/доставка, а также хранение всей истории статусов (трекинг) до выдачи клиенту.

**Сущности**:
- `Shipment` (Id, OrderId, TrackingNumber, CurrentStatus, CreatedAt).
- `AcceptanceInspection` (Id, ShipmentId, InspectedByUserId, PackagingCondition: Целая/Повреждена, CargoCondition: Целый/Повреждён, Comment, PhotoFileIds[], InspectedAt).
- `PackagingService` (Id, ShipmentId, Type, PerformedByUserId, PerformedAt) — фактически выполненная упаковка.
- `ShipmentStatusHistory` (Id, ShipmentId, Status, ChangedAt, Location/Warehouse, Comment)
  - Статусы: `Создана`, `Принята`, `НаСкладе`, `ВПути`, `Задерживается`, `ПрибылаВГородНазначения`, `ГотовК Выдаче`, `Выдана`, `Проблема`.

**API**: `POST /shipments/{id}/accept` (приёмка + фиксация состояния), `POST /shipments/{id}/status`, `GET /shipments/{id}`, `GET /track/{trackingNumber}` (публичный трекинг без авторизации), `POST /shipments/{id}/photos`.

**События**:
- Подписывается на `OrderConfirmed` (создаёт `Shipment`).
- Публикует `CargoAccepted`, `CargoStatusChanged`, `CargoPhotoUploaded` (→ AI Inspection), `CargoDelivered`.
- Подписывается на `PackageIntegrityAssessed` (результат от AI Inspection Service) — добавляет в акт приёмки как доп. проверку/алерт для сотрудника при расхождении с человеческой оценкой.

---

### 2.6 Notification Service — уведомления о статусе груза
**Назначение**: пункт 4 — уведомления клиенту (и, опционально, сотрудникам).

**Сущности**: `NotificationTemplate` (Code, Channel, Subject, Body), `NotificationLog` (Id, RecipientContact, Channel, Status, SentAt, RelatedEntity).

**Каналы**: Email (SMTP/SendGrid), SMS (Twilio/смс-провайдер), Push (веб/моб.), опционально Telegram-бот.

**API**: `GET /notifications?clientId=` (история уведомлений), `POST /notifications/preferences` (какие каналы использовать).

**События (подписки)**: `OrderCreated`, `OrderConfirmed`, `CargoAccepted`, `CargoStatusChanged` (Принят/В пути/Задерживается/Выдан), `PaymentCompleted`, `DocumentGenerated`.

---

### 2.7 AI Inspection Service — визуальная оценка целостности упаковки
**Назначение**: пункт 5 — интеграция с ИИ (CV-моделью) для автоматической оценки целостности упаковки/груза по фото, дополняющая оценку сотрудника.

**Сущности**: `InspectionJob` (Id, ShipmentId, PhotoFileIds[], Status: Queued/Processing/Completed/Failed), `InspectionResult` (Id, JobId, PackagingIntegrityScore, DamageDetected: bool, DamageType[], Confidence, ModelVersion, RawResponse).

**Реализация**: сервис как обёртка (façade) над ML-моделью — вызывает внешний/встроенный CV-инференс (например, ONNX Runtime с моделью классификации повреждений упаковки, либо внешний API компьютерного зрения). Хранит фото через File Storage Service.

**API**: `POST /inspections` (ручной запуск по shipmentId + фото), `GET /inspections/{id}`.

**События**: подписывается на `CargoPhotoUploaded`, публикует `PackageIntegrityAssessed` (→ Cargo Service, Notification Service при обнаружении повреждения).

---

## 3. Дополнительные сервисы (расширение MVP)

Помимо пяти обязательных пунктов, для полноценной работы системы предлагаются следующие компоненты:

### 3.1 Document Service — генерация документов
Формирование транспортной накладной, акта приёма-передачи, акта осмотра при повреждении, QR/штрихкода трек-номера (PDF). Подписывается на `CargoAccepted`, `CargoDelivered`; публикует `DocumentGenerated`. Хранит файлы через File Storage Service.

### 3.2 Payment Service — оплата услуг
Приём оплаты за заявку (картой/онлайн-эквайринг), статус оплаты, возвраты при отмене заявки. Подписывается на `OrderConfirmed`; публикует `PaymentCompleted`, `PaymentFailed`, `RefundIssued`.

### 3.3 Logistics Service — склады, транспорт, маршруты, курьеры
Реестр складов/терминалов по городам, назначение курьеров на забор/доставку, построение маршрута между городом отправления и городом назначения (в т.ч. через промежуточные склады), учёт транспортных средств и загрузки рейсов. Тесно связан с услугой «забор/доставка» из п.2. Подписывается на `CargoAccepted` (когда нужен забор), публикует `PickupAssigned`, `DeliveryAssigned`, `RouteUpdated` → Cargo Service обновляет статус/локацию.

### 3.4 File Storage Service
Единая точка хранения бинарных файлов (фото груза/упаковки, сканы документов) поверх S3-совместимого хранилища (MinIO), выдача presigned URL. Используется Cargo Service, AI Inspection Service, Document Service.

### 3.5 Reporting/Analytics Service
Агрегация данных из событий (заявки, выручка, среднее время доставки, процент повреждений, загрузка складов) в собственное read-хранилище (CQRS read model) для дашбордов менеджмента.

### 3.6 API Gateway (инфраструктурный компонент, не бизнес-сервис)
Единая точка входа (YARP/Ocelot), маршрутизация к сервисам, агрегация ответов для BFF (например, «страница заявки» = данные Orders + Cargo + Pricing), проверка JWT, rate limiting.

### 3.7 Другие полезные фичи
- **Публичный трекинг без авторизации** по номеру заявки/трек-номеру (уже заложен в Cargo Service `/track/{trackingNumber}`).
- **Отзывы/оценка сервиса** клиентом после выдачи груза (мини-сервис или часть Notification/Orders).
- **Калькулятор стоимости на сайте** без создания заявки — публичный эндпоинт `Pricing.calculate`.
- **SLA-мониторинг сроков доставки**: фоновая джоба в Cargo Service, сравнивающая `DeliveryDeadline` с текущим статусом и автоматически переводящая груз в статус «Задерживается» + уведомление.
- **Многоязычность уведомлений** (RU/EN) в Notification Service.
- **Аудит действий сотрудников** (кто и когда изменил статус/оценку) — общий Audit-лог, либо часть каждого сервиса.

---

## 4. Контракты событий RabbitMQ (сводно)

| Событие | Издатель | Подписчики |
|---|---|---|
| `UserRegistered` | Identity | Clients |
| `OrderCreated` | Orders | Notification, Reporting |
| `OrderConfirmed` | Orders | Cargo, Payment, Notification |
| `OrderCancelled` | Orders | Payment, Notification |
| `TariffChanged` | Pricing | Orders (инвалидация кэша) |
| `CargoAccepted` | Cargo | Notification, Document, Logistics |
| `CargoStatusChanged` | Cargo | Orders, Notification, Reporting |
| `CargoPhotoUploaded` | Cargo | AI Inspection |
| `PackageIntegrityAssessed` | AI Inspection | Cargo, Notification |
| `CargoDelivered` | Cargo | Orders, Notification, Document, Reporting |
| `PaymentCompleted` / `PaymentFailed` | Payment | Orders, Notification |
| `DocumentGenerated` | Document | Notification, Orders |
| `PickupAssigned` / `DeliveryAssigned` / `RouteUpdated` | Logistics | Cargo |

Формат сообщений — versioned JSON-контракты (`CargoService.Contracts` — общая NuGet/shared-библиотека DTO событий, без бизнес-логики, подключаемая во все сервисы).

---

## 5. Список задач для разработки

### Фаза 0 — Инфраструктура репозитория и окружения
- [x] Реорганизовать репозиторий в монорепо: `services/{service-name}/` для каждого микросервиса, `shared/CargoService.Contracts/` для общих событийных DTO.
- [x] Перенести существующий код (`CargoService.*`) в `services/identity-service/`. После убедиться, что в монорепо остались только папки вида `services/{service-name}/`.
- [x] Создать файл CLAUDE.md и заполнить его необходимой информацией.
- [x] Настроить единый `docker-compose.yml`: PostgreSQL (по контейнеру/схеме на сервис), RabbitMQ (с management UI), MinIO, все сервисы, API Gateway.
- [x] Настроить `.editorconfig`, единый стиль кода, Directory.Build.props для общих версий пакетов.
- [x] Создать `CargoService.Contracts` (события, версия v1, соглашения об именовании очередей/exchange).
- [x] Настроить общий шаблон `docker-compose.override.yml` для локальной разработки (hot reload, миграции при старте).
- [x] Подключить централизованное логирование (Serilog → Seq) во всех сервисах.

### Фаза 1 — Identity Service
- [x] Добавить роли и авторизацию по ролям (`Client`, `Manager`, `WarehouseOperator`, `Courier`, `Admin`).
- [x] ~~Реализовать хэширование пароля~~ — заменено интеграцией с Keycloak: realm/client/роли заведены как код (`docker/keycloak/realm-export.json`, импортируется при старте контейнера), API валидирует JWT, выпущенные Keycloak (`IdentityService.Infrastructure.Keycloak`), локальный пароль больше не хранится вовсе (миграция `DropUserPassword`) — Keycloak единственный держатель учётных данных. Регистрация/создание сотрудника создаёт пользователя в Keycloak через Admin API (`IIdentityProviderClient`) и назначает realm-роль.
- [x] Реализовать `POST /auth/login` — теперь означает прокси к Keycloak token endpoint (Resource Owner Password Credentials, `directAccessGrantsEnabled` уже включён в realm-export) с выдачей access/refresh токенов, а не самостоятельную выдачу JWT сервисом.
- [x] Реализовать `POST /auth/refresh` — аналогично, прокси к Keycloak token endpoint с `grant_type=refresh_token`.
- [x] Публикация события `UserRegistered` через Outbox.
- [x] Настроить EF Core миграции и Persistence для PostgreSQL (частично есть — проверить/дополнить).
- [x] Unit-тесты Application, интеграционные тесты API (Testcontainers). Интеграционные тесты не запускались вживую в этой среде — нет доступного Docker-демона; прогнать `dotnet test` локально перед тем, как полагаться на них в CI.
- [x] Dockerfile + подключение в docker-compose. Dockerfile и подключение в `docker-compose.yml` были сделаны ещё в Фазе 0, но с тех пор `IdentityService.Application` обзавёлся зависимостью на `shared/CargoService.Contracts` (задача «Outbox»), которая лежит вне `services/identity-service` — старый build-контекст не мог её достать. Контекст сборки перенесён на корень репозитория (Dockerfile/override обновлены), сам образ собрать вживую не удалось (нет Docker-демона в этой среде), но идентичные `dotnet restore`/`dotnet publish` из корня репозитория прошли успешно и подтвердили, что граф проектов резолвится.

### Фаза 2 — Clients Service (CRM контрагентов)
- [x] Создать проект (Domain/Application/Persistence/Infrastructure/API).
- [x] Сущности `Counterparty`, `ClientAccount`, миграции PostgreSQL.
- [x] CRUD API + поиск по городу/названию/телефону.
- [x] Consumer события `UserRegistered` → авто-создание `ClientAccount`.
- [x] Тесты, Dockerfile, docker-compose. Unit-тесты `ClientsService.Application.Tests` (8 тестов) прогнаны и зелёные. Интеграционные тесты `ClientsService.IntegrationTests` написаны по образцу identity-service (Testcontainers, `TestAuthHandler` с доп. заголовком `X-Test-User-Id`), но не запускались вживую в этой среде — Npgsql падает с ошибкой аутентификации против Testcontainers-контейнера Postgres на этой машине; воспроизвели ту же ошибку и на немодифицированных тестах identity-service, значит это окружение-специфичное ограничение (Docker Desktop/Windows), а не баг в коде — прогнать `dotnet test` в другой среде/CI перед тем, как полагаться на них. Сам сервис проверен вживую другим способом: собранный `final`-образ (`docker build --target final`, затем `docker compose up`) поднимается, мигрирует БД и отвечает на `/openapi/v1.json` — через контейнеры на сети `cargoservice_default`, как и остальные задачи Фазы 2.

### Фаза 3 — Pricing Service
- [x] Создать проект по Clean Architecture.
- [x] Сущность `TariffRate`, seed начальных тарифов (обычная/экспресс, деревянная/паллет/спец. упаковка, забор/доставка, страхование).
- [x] Реализовать `POST /pricing/calculate` с бизнес-логикой расчёта (вес/объём/расстояние/услуги).
- [x] Admin API для управления тарифами + публикация `TariffChanged`.
- [ ] ~~Кэширование тарифов (in-memory/Redis) на стороне Orders Service~~ — перенесено на Фазу 4 (см. её чек-лист): Orders Service ещё не существует на момент Фазы 3, строить кэш и consumer `TariffChanged` негде без самого сервиса.
- [x] Тесты расчёта (граничные случаи: нулевой вес, комбинации услуг), Dockerfile. Unit-тесты `PricingService.Application.Tests` (11 тестов — расчёт + управление тарифами) прогнаны и зелёные. Интеграционные тесты `PricingService.IntegrationTests` написаны (по образцу clients-service), но не запускались вживую в этой среде — тот же известный Npgsql/Testcontainers-баг окружения, что и у identity-service/clients-service (см. их записи в чек-листе). Сервис проверен вживую иначе: собранный `final`-образ (`docker build --target final`, затем `docker compose up`) поднимается, мигрирует БД (`tariff_rates`, `outbox_messages`) и отвечает на `/openapi/v1.json`.

### Фаза 4 — Orders Service
- [x] Создать проект по Clean Architecture.
- [x] Сущность `Order` со всеми полями из ТЗ (см. 2.4), валидация (FluentValidation). Миграция применена к реальному Postgres и проверена — все owned-типы (`Sender`/`Recipient`/`ServiceOptions`) корректно легли в колонки таблицы `orders`; сервис поднимается и отвечает на `/openapi/v1.json`.
- [x] Синхронный вызов `Pricing.calculate` при создании заявки (HttpClient + Polly retry/circuit breaker). Сам вызов из `POST /orders` — следующая задача; здесь заведён только клиент (`IPricingClient`/`PricingClient`) с типизированным `HttpClient` и Polly (retry с экспоненциальным backoff, circuit breaker, таймаут на отдельную попытку — таймаут на весь `HttpClient` специально сделан большим "предохранителем", а не основным ограничителем, иначе он обрезает retry-последовательность на середине, что подтвердилось на живом тесте).
- [x] Consumer `TariffChanged` → кэширование тарифов (in-memory/Redis), обновление по событию вместо повторного синхронного вызова `Pricing.calculate` на каждую мелочь (перенесено из Фазы 3). Реализовано как кэш результатов `calculate()` (не сырых тарифов — см. обсуждение в задаче), т.к. `GET /tariffs` требует роль Admin/Manager, а Orders Service действует от имени клиента: `ICalculationCache`/`InMemoryCalculationCache` (IMemoryCache, ключ — вход расчёта), `PricingClient` проверяет кэш перед HTTP-вызовом, `TariffChangedConsumer` сбрасывает кэш целиком при получении события.
- [x] `POST /orders`, подтверждение/отмена заявки, генерация номера заявки. `ClientAccountId` хранит Identity `UserId` (claim `sub`) напрямую, без синхронного вызова в clients-service. Добавлены `DistanceKm`/`CargoVolumeM3` в `Order`/`CreateOrderRequest` — сервис не знает гео/объём, клиент передаёт их сам (как и в `PriceCalculationRequest`). Номер — `"{yyyyMMdd}-{6 букв/цифр}"`, уникальный индекс в БД. Проверено живым прогоном на `cargoservice_default`: без токена → 401; с `Admin` → 403; с `Client` → 201 с сгенерированным номером и `CalculatedPrice`, совпадающим с прямым вызовом `pricing.calculate` на те же параметры; `GET {id}` тем же клиентом → 200, другим клиентом → 404 (владение); `confirm` → 200 `Confirmed`, повторный `confirm` идемпотентен; `cancel` на подтверждённой → 204 `Cancelled`; `confirm` после `cancel` → 409. Тестовые данные (пользователи, заявка) удалены после проверки. Вне рамок задачи — outbox/публикация событий (следующая задача).
- [x] Outbox + публикация `OrderCreated`/`OrderConfirmed`/`OrderCancelled`. Реализовано по образцу identity-service/pricing-service: `OutboxMessage`/`OutboxMessageConfiguration` + `OutboxWriter`/`OutboxReader` (Persistence, таблица `outbox_messages`) + `OutboxDispatcher` (Infrastructure `BackgroundService`, опрос раз в 5с, паблиш в `cargoservice.events`). `OrdersService.cs` ставит событие в очередь на том же `DbContext` непосредственно перед вызовом репозитория, который делает `SaveChangesAsync` (create/confirm/cancel) — событие и бизнес-изменение коммитятся одной транзакцией; идемпотентные no-op переходы новых событий не публикуют. `OrderCancelled.Reason` не заполняется — API не собирает причину отмены (не входит в текущий контракт `Order`). **Найден и исправлен баг живым прогоном**: `Order.Id` генерируется EF Core только во время `SaveChangesAsync`, а `OrderCreated` строился и сериализовался до вызова репозитория — в событие уходил `OrderId` из одних нулей. Исправлено явной генерацией `order.Id = Guid.NewGuid()` в начале `CreateAsync` до постановки события в очередь. Проверено живым прогоном на `cargoservice_default`: временная очередь, привязанная к `orders-service.#` на `cargoservice.events`, полный цикл create→confirm→cancel — все три события получены с корректными `OrderId`/полями, все три строки `outbox_messages` помечены `ProcessedAtUtc`; повторный `confirm` (409) новых строк не создал. Тестовые данные удалены после проверки.
- [x] Consumer `CargoStatusChanged`, `PaymentCompleted` — обновление статуса заявки в read-модели. `CargoStatusChangedConsumer`/`PaymentCompletedConsumer` (Infrastructure, по образцу `TariffChangedConsumer`, каждый со своей очередью `orders-service.{event}`/DLQ), пишут через новый системный `IOrdersRepository.GetByIdAsync(id, ct)` (без фильтра по владельцу — используется только event-консьюмерами, не HTTP-эндпоинтами) и `IOrdersService.UpdateCargoStatusAsync`/`MarkPaidAsync`. **Обнаружен и закрыт архитектурный пробел**: `CargoStatusChanged` в `CargoService.Contracts` нёс только `ShipmentId`/`TrackingNumber`/`Status`/`Location`, без `OrderId` — Orders Service не был подписан ни на одно событие, дающее связку `TrackingNumber↔OrderId` (`CargoAccepted` её содержит, но не адресован orders-service). Добавлено обязательное поле `OrderId` в контракт (cargo-service ещё не реализован, обратной совместимости ломать не у кого — при реализации в Фазе 5 просто прокинет `Shipment.OrderId`, которое у него и так уже есть). В `Order` добавлены read-model поля `TrackingNumber`/`CargoStatus` (из `CargoStatusChanged`) и `IsPaid`/`PaymentId` (из `PaymentCompleted`) — не входят в исходный список полей ТЗ §2.4, тот же тип пробела, что уже закрывался `DistanceKm`/`CargoVolumeM3` в задаче 5; отражены в `GET /orders/{id}` (кроме `PaymentId` — не полезен клиенту). Проверено живым прогоном: события опубликованы напрямую в `cargoservice.events` с нужными routing key (имитация ещё не построенных cargo-service/payment-service) — `TrackingNumber`/`CargoStatus`/`IsPaid` на заявке обновились корректно; событие с несуществующим `OrderId` залогировано как warning и удалено (не ушло в DLQ — это перманентное несоответствие, а не временный сбой); обе очереди и их DLQ пусты после прогона. Тестовые данные удалены после проверки.
- [x] Личный кабинет клиента: `GET /orders` с фильтрами/пагинацией. Чисто self-service для роли `Client` — `ClientAccountId` всегда берётся из JWT `sub` (как и везде в сервисе), без query-параметра `clientId` и без Admin/Manager-доступа к чужим заявкам (осознанное отличие от буквальной формулировки API в §2.4). Фильтр — только `Status` (опционально); пагинация — `page`/`pageSize` (по умолчанию 1/20, `pageSize` ограничен 1–100 через `GetOrdersRequestValidator`), ответ — `OrderListResponse { Items, TotalCount, Page, PageSize }` (первый paginated-эндпоинт в монорепозитории, общей обёртки-конвенции пока нет — сделан специфичный DTO, не generic). Сортировка — по `CreatedAt` убыв. Проверено живым прогоном: без токена → 401; `Admin` → 403; `Client` без фильтров → все 3 свои заявки с `totalCount=3`; `status=Confirmed`/`status=Cancelled` → по 1 записи; второй клиент без заявок → `totalCount=0`; `pageSize=2` корректно разбивает на страницы (page=1 → 2 записи, page=2 → 1); `page=0`/`pageSize=200` → `400` от валидатора. Тестовые данные удалены после проверки.
- [x] Тесты, Dockerfile. `OrdersService.Application.Tests` (xUnit + Moq, 15 тестов на `OrdersService` — генерация номера/сборка `PriceCalculationRequest`/enqueue `OrderCreated` до сохранения, делегирование `GetByIdAsync`/`GetByClientAsync`, стейт-машина confirm/cancel с idempotent no-op и conflict, `UpdateCargoStatusAsync`/`MarkPaidAsync`). `OrdersService.IntegrationTests` (xUnit + `Mvc.Testing` + Testcontainers, `TestAuthHandler` с `X-Test-User-Id` для владения по образцу clients-service, `FakePricingClient` вместо реального HTTP-вызова pricing-service по образцу `FakeIdentityProviderClient`) — 13 тестов на `OrdersController` (401/403/400/404/409, идемпотентный confirm, владение, фильтр/пагинация `GET /orders`, прямая проверка БД на запись outbox-сообщения в той же транзакции). Testcontainers/Npgsql падают в этом окружении с той же `28P01`-ошибкой, что и у identity-service/clients-service/pricing-service — подтверждённое окруженческое ограничение Windows/Docker Desktop, не баг кода. `Dockerfile` — тот же dev/final паттерн (контекст сборки — корень репозитория из-за `shared/CargoService.Contracts`), добавлены записи `ordersservice` в `docker-compose.yml` (порт 8084, `PricingService__BaseUrl=http://pricingservice:8080`, зависимость от `pricingservice`) и `docker-compose.override.yml` (dev-стадия). Program.cs получил `public partial class Program;` для `WebApplicationFactory<Program>`. Проверено: unit-тесты — 15/15 зелёные; финальный (production) образ собирается и публикуется через `docker build --target final`; полный прогон через реальный `docker compose up` (а не изолированный контейнер, как в предыдущих задачах) — `POST /orders` → 201, `confirm` → 200, `GET /orders` → `totalCount=1`, оба outbox-события (`order-created`/`order-confirmed`) доставлены в `cargoservice.events` и помечены обработанными. Тестовые данные удалены после проверки.

### Фаза 5 — Cargo Service (приёмка и трекинг)
- [x] Создать проект по Clean Architecture. Пять слоёв по тому же образцу, что у остальных сервисов (extension-методы вместо логики в `Program.cs`, Serilog→Seq, EF Core + Npgsql с миграцией при старте, `AppDbContextFactory` для `dotnet ef`, явный `TypeAdapterConfig.GlobalSettings.Scan()` в `AddApiServices`). **Коллизия имён**: конвенция `{сервис}service` дала бы `cargoservice`, но это имя занято identity-service (наследие названия платформы) — БД/пользователь Postgres и будущий compose-сервис названы **`cargoshipments`**, порт для compose зарезервирован 8085. Сами проекты/namespace обычные (`CargoService.Domain` и т.д.) и с shared-проектом `CargoService.Contracts` не конфликтуют — проверено сборкой. Проверено живым прогоном: сервис поднимается, применяет миграции (создаёт `__EFMigrationsHistory` в БД `cargoshipments`) и отвечает на `/openapi/v1.json` с заголовком `CargoService.API | v1`. На старте логируются `WRN` об отсутствии `IEntityTypeConfiguration` и `ERR` на пробном чтении `__EFMigrationsHistory` — оба ожидаемы на этапе скаффолда (сущностей и миграций ещё нет). Keycloak-аутентификация появится вместе с первым эндпоинтом (задача 4), как и в orders-service.
- [x] Сущности `Shipment`, `AcceptanceInspection`, `PackagingService`, `ShipmentStatusHistory`. Все четыре с полями из ТЗ (см. 2.5) + enum'ы `ShipmentStatus` (9 значений жизненного цикла), `PackagingCondition`/`CargoCondition` (намеренно раздельные, хотя значения сегодня совпадают — это разные предметы оценки и расширяться будут независимо), `PackagingType` (значения совпадают с orders-service: в заявке — желаемая упаковка, здесь — фактически выполненная; общего типа нет сознательно, «база на сервис»). Связи — по образцу clients-service: коллекции-навигации на родителе, у детей только `ShipmentId`, каскадное удаление. `PhotoFileIds` лёг в нативную Postgres-колонку `uuid[]` (без отдельной таблицы и json — список короткий и читается всегда целиком вместе с актом); сами файлы будут в File Storage (Фаза 10). `CurrentStatus` на `Shipment` — сознательная денормализация «последнего статуса», чтобы трекинг и списки не брали максимум по истории на каждый запрос. Уникальный индекс на `OrderId` делает повторную доставку `OrderConfirmed` (RabbitMQ at-least-once) безобидной вместо создания второго `Shipment`; уникальный индекс на `TrackingNumber` — публичный идентификатор. Проверено на реальном Postgres: миграция применена, все 4 таблицы созданы, `PhotoFileIds` действительно `uuid[]` (массив из 2 GUID записался и прочитался), enum'ы легли строками, дубликат `OrderId` отвергается индексом, каскад чистит детей при удалении `Shipment`; сервис на этой схеме поднимается и отвечает `200` на `/openapi/v1.json` (стартовые `WRN` про отсутствие `IEntityTypeConfiguration` и `ERR` на чтении `__EFMigrationsHistory` из задачи 1 ушли). Тестовые строки удалены после проверки.
- [x] Consumer `OrderConfirmed` → создание `Shipment` с генерацией трек-номера. `OrderConfirmedConsumer` (Infrastructure, по образцу консьюмеров orders-service: своя очередь `cargo-service.order-confirmed` + DLQ, `BackgroundService` с переподключением через 10с) → `IShipmentsService.CreateFromConfirmedOrderAsync` → `IShipmentsRepository`. Груз заводится в статусе `Created` сразу с первой записью в `ShipmentStatusHistory` — одной транзакцией (EF обходит граф от корня), чтобы трекинг с самого начала показывал хронологию, а не пустой список рядом с уже выставленным `CurrentStatus`. Трек-номер — `CS-XXXXXXXXXX` (10 символов из алфавита без `0/O/1/I`, как у номера заявки): формат намеренно отличается от `{yyyyMMdd}-{6}` в orders-service — трек-номер публичный, дата в нём лишняя, а префикс не даёт спутать его с номером заявки. **Двухуровневая идемпотентность**, и второй уровень не избыточен: дешёвая проверка `ExistsByOrderIdAsync` отсекает обычную повторную доставку, уникальный индекс на `OrderId` закрывает гонку двух одновременных доставок. Проверку добавил после живого прогона, который показал, что без неё EF пишет **два `ERR` со стектрейсом на каждый дубликат** — при массовой переотправке после рестарта это залило бы Seq ложными ошибками. Нарушение индекса ловится в Persistence (по `SqlState` 23505, не по тексту сообщения) — Infrastructure остаётся без зависимости на EF/Npgsql. Проверено живым прогоном на реальных Postgres + RabbitMQ: очередь и привязка к `orders-service.order-confirmed` создаются при старте; событие → груз с трек-номером, статусом `Created` и записью истории; два повтора того же события → ни одного дубля в БД, только `INF`-строки, ни одного `ERR`; битый payload → уходит в DLQ, основная очередь пуста. Сквозной прогон с реальным orders-service не гонял: обе половины routing key вычисляются из общего `RabbitMqConventions`, а фактическое совпадение видно из проверки Фазы 4 (задача 6), где orders-service публиковал ровно `orders-service.order-confirmed` с этой же схемой payload. Тестовые данные и DLQ очищены после проверки.
- [x] API приёмки груза сотрудником: фиксация состояния упаковки/груза, фото (через File Storage), выполненные услуги упаковки. `POST /api/shipments/{id}/accept`, `POST /api/shipments/{id}/photos` и `GET /api/shipments/{id}` (полная карточка для сотрудников; клиентский урезанный вид — публичный трекинг, следующие задачи). Роли — `WarehouseOperator,Manager,Admin`: приёмка это работа склада, Manager/Admin допущены как надзорные; `Client` сюда не ходит. Вместе с первым эндпоинтом подключена Keycloak-аутентификация (копия паттерна orders-service, роли копируются без фильтрации — локального enum `Role` здесь нет). `InspectedByUserId` берётся из claim `sub`, а не из тела запроса, иначе сотрудник мог бы подписать акт чужим именем. **Фото — только идентификаторы файлов**: байты через cargo-service не проходят, File Storage (Фаза 10) хранит сами файлы, сюда приходят готовые `PhotoFileIds` (это и есть прочтение «через File Storage» из ТЗ, и единственный вариант, реализуемый до Фазы 10). Приёмка строго из статуса `Created` → `Accepted`: в отличие от смены статуса она не идемпотентна, повтор создал бы второй акт, поэтому повторная приёмка даёт `409`. Фото добавляются в последний акт, у непринятого груза — `409` (класть некуда); повторно присланные файлы отсекаются. Акт, услуги упаковки, новый статус и запись истории уходят одним `SaveChanges`. **Найден и исправлен баг живым прогоном**: дочерним сущностям проставлялся `Id = Guid.NewGuid()`, и при добавлении в коллекцию *уже отслеживаемого* родителя EF по заполненному ключу считал их существующими — генерировал `UPDATE` вместо `INSERT`, не находил строк и падал `DbUpdateConcurrencyException`. В задаче 3 это не проявлялось, потому что там родитель сам был новым и весь граф уходил как `Added`. Ключи дочерних сущностей отданы EF (и там, и здесь — чтобы паттерн не скопировали обратно в ловушку). `PhotoFileIds` при добавлении присваивается новым списком, а не мутируется на месте: увидит ли EF правку массива `uuid[]` «на месте», зависит от value comparer'а провайдера. Проверено живым прогоном на реальных Postgres + RabbitMQ + Keycloak с настоящими JWT: без токена → 401, `Client` → 403, `WarehouseOperator` → 200; приёмка → статус `Accepted`, акт с состоянием/комментарием/2 фото, 2 записи услуг упаковки, история из двух записей в хронологии, `inspectedByUserId` совпал с `sub` токена; повторная приёмка → 409; несуществующий груз → 404; добавление фото с дубликатом → в акте 3 уникальных файла; пустой список → 400 от валидатора; фото у непринятого груза → 409. Тестовые данные и пользователи удалены после проверки.
- [x] API изменения статуса + история статусов. `POST /api/shipments/{id}/status` (статус + опциональные `Location`/`Comment`); история отдаётся в составе `GET /api/shipments/{id}` в хронологическом порядке (отдельного эндпоинта нет — в списке API §2.5 его тоже нет). **Правила переходов сознательно свободные**: жёсткого графа нет, потому что реальная логистика не линейна (груз возвращается на склад, задерживается, снова уезжает), и строгий автомат мешал бы складу отражать факты. Ограничений всего два, и они разнесены по природе: недопустимые сами по себе значения `Created`/`Accepted` режет валидатор запроса (`400`) — их ставят только создание груза по `OrderConfirmed` и эндпоинт приёмки, иначе статус `Accepted` можно было бы получить в обход акта; зависящее от состояния «из `Delivered` переходов нет» проверяет сервис (`409`, терминальный статус). **Повтор того же статуса разрешён намеренно**: «ВПути / Москва», затем «ВПути / Владимир» — две законные отметки трекинга, а не дубликат, поэтому идемпотентного no-op здесь нет и каждый вызов дописывает запись в историю. Роли: смену статуса ставит ещё и `Courier` (забор и доставка — его часть маршрута), но к приёмке с составлением акта он не допущен; ради этого авторизация переехала с уровня класса на уровень действий — атрибуты класса и метода складываются через И, и расширить набор ролей на одном действии иначе нельзя. Проверено живым прогоном на реальных Postgres + RabbitMQ + Keycloak: полный маршрут `InWarehouse → InTransit(Москва) → InTransit(Владимир) → Delayed → ArrivedAtDestination → ReadyForPickup → Delivered` частью от склада, частью от курьера — все `200`, история из 9 записей в хронологии с локациями и комментариями; ручная установка `Created`/`Accepted` → `400` с текстом правила; курьер на приёмке → `403`; без токена → `401`; несуществующий груз → `404`; любой статус после `Delivered` → `409`. Тестовые данные и пользователи удалены после проверки.
- [x] Публичный `GET /api/track/{trackingNumber}` без авторизации (ограниченный набор полей). Отдельный `TrackingController` с `[AllowAnonymous]` — единственный анонимный эндпоинт сервиса. **Наружу отдаются только** `TrackingNumber`, `CurrentStatus`, `CreatedAt` и лента `{Status, ChangedAt, Location}`. Сознательно НЕ отдаются: `Id`/`OrderId` (внутренние идентификаторы, второй ещё и ведёт в чужой сервис), акты приёмки (`InspectedByUserId` — личность сотрудника, состояние груза, файлы фото), услуги упаковки (`PerformedByUserId`) и **комментарии в истории** — их пишет склад для себя, и внутренняя пометка не должна утечь тому, кто просто знает трек-номер. Маппинг перечисляет поля поимённо с `IgnoreNonMapped(true)`, а не полагается на совпадение имён: так новое поле в `Shipment` не утечёт наружу само собой — для анонимного эндпоинта это важнее краткости. Трек-номер нормализуется (регистр + пробелы): его вбивают руками, а хранится он в верхнем регистре. Проверено живым прогоном: груз с намеренно «чувствительными» данными (комментарии `ВНУТРЕННЯЯ ПОМЕТКА СКЛАДА` в акте и в истории, фото, `OrderId`) — анонимный ответ прошёл 11 проверок на утечку (ни комментариев, ни `orderId`, ни `inspections`/`packagingServices`, ни `photoFileIds`, ни внутренних `Id`), при этом сотруднику через `GET /api/shipments/{id}` те же поля видны — то есть данные отфильтрованы, а не потеряны; номер в нижнем регистре и с пробелами → `200`, несуществующий и мусорный → `404`. Не реализовано: ограничение частоты запросов к анонимному эндпоинту (перебор трек-номеров непрактичен — 32^10 вариантов, — но защита от нагрузки уместнее на API Gateway, Фаза 12). Тестовые данные и пользователь удалены после проверки.
- [x] Публикация `CargoAccepted`, `CargoStatusChanged`, `CargoPhotoUploaded`, `CargoDelivered` (Outbox). Инфраструктура outbox — копия orders-service (`OutboxMessage`/`OutboxWriter`/`OutboxReader` + `OutboxDispatcher`, опрос раз в 5с, таблица `outbox_messages`); события ставятся в очередь на том же `DbContext` перед `SaveChanges`, поэтому бизнес-изменение и событие коммитятся одной транзакцией. Раскладка событий по операциям: **приёмка** публикует сразу три — `CargoAccepted`, `CargoStatusChanged` (переход в `Accepted`) и, если фото приложены к акту, `CargoPhotoUploaded`. Дублирование `CargoAccepted`+`CargoStatusChanged` намеренное: у событий разные подписчики (`CargoAccepted` → notification/document/logistics, `CargoStatusChanged` → orders-service/notification/reporting), и без второго orders-service никогда не увидел бы переход в `Accepted` в read-модели заявки. **Смена статуса** публикует `CargoStatusChanged`, а на `Delivered` дополнительно `CargoDelivered` — у выдачи свой круг подписчиков, которым не нужно разбирать строковый статус. **Добавление фото** публикует `CargoPhotoUploaded` только с *новыми* файлами и не публикует ничего, если все присланные уже в акте, — иначе ai-inspection-service анализировал бы одно и то же повторно. `CargoDelivered.ReceivedByName` не заполняется: отдельного эндпоинта выдачи нет, статус ставится обычным `POST /status` (тот же компромисс, что с `OrderCancelled.Reason`). Состояния и статусы возятся строками — подписчики живут в других сервисах и наших enum'ов не знают. Проверено живым прогоном: полный цикл дал ровно 7 строк outbox, все помечены обработанными, payload'ы корректны, повторное добавление одних дубликатов события не породило. **Сквозной кросс-сервисный прогон** (первый в проекте, где цепочка замкнулась целиком): клиент создал заявку в orders-service → подтвердил → outbox orders-service опубликовал `OrderConfirmed` → cargo-service завёл груз `CS-LQ4LZT4Y7S` → сотрудник склада принял груз → outbox cargo-service опубликовал `CargoAccepted`/`CargoStatusChanged` → orders-service обновил read-модель заявки (`trackingNumber` и `cargoStatus` из `null` стали `CS-LQ4LZT4Y7S`/`Accepted`) → после `InTransit`/`Delivered` заявка показала `cargoStatus: Delivered`, а анонимный трекинг — всю ленту из 4 статусов. Это же подтвердило решение Фазы 4 о добавлении `OrderId` в контракт `CargoStatusChanged`: без него сопоставить событие с заявкой было бы нечем. Тестовые данные, очередь и пользователи удалены после проверки.
- [ ] Consumer `PackageIntegrityAssessed` — сопоставление с оценкой сотрудника, флаг расхождения.
- [ ] Фоновая джоба контроля SLA сроков доставки → авто-статус «Задерживается».
- [ ] Тесты, Dockerfile.

### Фаза 6 — Notification Service
- [ ] Создать проект по Clean Architecture.
- [ ] Шаблоны уведомлений (Code/Channel/Subject/Body), сущность истории `NotificationLog`.
- [ ] Интеграция с email-провайдером (SMTP/SendGrid) и SMS-провайдером.
- [ ] Consumers всех статусных событий (см. таблицу в разделе 4).
- [ ] Идемпотентность обработки (Inbox по MessageId) — чтобы не дублировать уведомления.
- [ ] API истории уведомлений клиента + настройка предпочитаемых каналов.
- [ ] Тесты, Dockerfile.

### Фаза 7 — AI Inspection Service
- [ ] Создать проект по Clean Architecture.
- [ ] Выбрать/подготовить модель компьютерного зрения (классификация «упаковка цела/повреждена», детекция повреждений) — ONNX-модель локально либо внешний CV API.
- [ ] Сущности `InspectionJob`, `InspectionResult`.
- [ ] Consumer `CargoPhotoUploaded` → постановка в очередь на инференс.
- [ ] Инференс-пайплайн (загрузка фото из File Storage → препроцессинг → модель → результат).
- [ ] Публикация `PackageIntegrityAssessed` (Outbox).
- [ ] Ручной API запуска повторной проверки `POST /inspections`.
- [ ] Метрики качества модели (confusion matrix на тестовом наборе, confidence threshold).
- [ ] Тесты (мок модели), Dockerfile.

### Фаза 8 — Document Service
- [ ] Создать проект по Clean Architecture.
- [ ] Генерация PDF: накладная, акт приёма-передачи, акт осмотра при повреждении.
- [ ] Генерация QR/штрихкода трек-номера.
- [ ] Consumers `CargoAccepted`, `CargoDelivered` → авто-генерация нужных документов.
- [ ] Хранение сгенерированных файлов через File Storage Service, публикация `DocumentGenerated`.
- [ ] API получения документа по заявке/shipment.
- [ ] Тесты, Dockerfile.

### Фаза 9 — Payment Service
- [ ] Создать проект по Clean Architecture.
- [ ] Интеграция с платёжным провайдером (эквайринг/платёжная ссылка).
- [ ] Consumer `OrderConfirmed` → выставление счёта.
- [ ] Обработка webhook от платёжного провайдера, публикация `PaymentCompleted`/`PaymentFailed`.
- [ ] Возвраты при `OrderCancelled`.
- [ ] Тесты (мок провайдера), Dockerfile.

### Фаза 10 — Logistics Service
- [ ] Создать проект по Clean Architecture.
- [ ] Сущности: `Warehouse`, `Vehicle`, `Courier`, `Route`, `PickupTask`, `DeliveryTask`.
- [ ] Consumer `CargoAccepted` → создание задачи забора/доставки при выбранной услуге.
- [ ] API назначения курьера/маршрута, обновление статуса задачи.
- [ ] Публикация `PickupAssigned`, `DeliveryAssigned`, `RouteUpdated`.
- [ ] Тесты, Dockerfile.

### Фаза 11 — File Storage Service
- [ ] Развернуть MinIO (S3-совместимое), создать сервис-обёртку (Application+API) с presigned URL upload/download.
- [ ] API загрузки фото/документов, лимиты по размеру/типу файла, антивирус-скан (опционально).
- [ ] Интеграция клиентских SDK/HttpClient во все сервисы, которым нужны файлы (Cargo, AI Inspection, Document).

### Фаза 12 — API Gateway
- [ ] Развернуть YARP-based Gateway, маршрутизация ко всем сервисам.
- [ ] Проверка JWT на уровне Gateway, проброс claims вниз (заголовки/трейс).
- [ ] Rate limiting, базовый WAF/защита от абьюза публичных эндпоинтов (трекинг, калькулятор).
- [ ] Агрегированные BFF-эндпоинты для UI (например, полная карточка заявки).

### Фаза 13 — Наблюдаемость и DevOps
- [ ] OpenTelemetry трассировка между сервисами (через RabbitMQ + HTTP), экспорт в Jaeger/Tempo.
- [ ] Метрики (Prometheus) + дашборды (Grafana): очередь сообщений, latency, ошибки.
- [ ] Health checks (`/health/live`, `/health/ready`) во всех сервисах, интеграция с docker-compose healthcheck.
- [ ] Централизованная обработка ошибок и retry/DLQ-политика для RabbitMQ consumers.
- [ ] CI/CD: сборка и публикация Docker-образов по сервисам, разделение пайплайнов (только изменённый сервис пересобирается).
- [ ] Секреты/конфигурация через переменные окружения / .env, не хардкодить строки подключения.

### Фаза 14 — Сквозное тестирование
- [ ] End-to-end сценарий: регистрация клиента → создание заявки → расчёт стоимости → приёмка груза → оценка ИИ → изменение статусов → уведомления → выдача груза.
- [ ] Нагрузочное тестирование очередей RabbitMQ и API Gateway.
- [ ] Проверка идемпотентности при повторной доставке сообщений (RabbitMQ redelivery).

---

## 6. Рекомендуемый порядок реализации (MVP-first)

1. Фаза 0 (инфраструктура) → Фаза 1 (Identity, уже частично готов).
2. Фаза 3 (Pricing) — нужен до Orders, т.к. используется синхронно при создании заявки.
3. Фаза 2 (Clients) — параллельно с Pricing.
4. Фаза 4 (Orders) → Фаза 5 (Cargo) — ядро бизнес-процесса.
5. Фаза 11 (File Storage) — нужен до AI Inspection и Document.
6. Фаза 6 (Notification) — подключается по мере появления событий.
7. Фаза 7 (AI Inspection) — после того, как Cargo умеет принимать фото.
8. Фазы 8–10 (Document, Payment, Logistics) — расширение после стабилизации MVP.
9. Фаза 12 (API Gateway) — можно поднять рано (простой прокси) и наращивать по ходу.
10. Фаза 13–14 — сквозные, ведутся параллельно с самого начала в облегчённом виде, углубляются к концу.
