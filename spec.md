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

**API**: `POST /orders`, `GET /orders/{id}`, `GET /orders?clientId=`, `PUT /orders/{id}`, `POST /orders/{id}/confirm`, `POST /orders/{id}/cancel`.

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
- [ ] Настроить `.editorconfig`, единый стиль кода, Directory.Build.props для общих версий пакетов.
- [ ] Создать `CargoService.Contracts` (события, версия v1, соглашения об именовании очередей/exchange).
- [ ] Настроить общий шаблон `docker-compose.override.yml` для локальной разработки (hot reload, миграции при старте).
- [ ] Подключить централизованное логирование (Serilog → Seq) во всех сервисах.

### Фаза 1 — Identity Service
- [ ] Добавить роли и авторизацию по ролям (`Client`, `Manager`, `WarehouseOperator`, `Courier`, `Admin`).
- [ ] Реализовать хэширование пароля (сейчас хранится как есть — заменить на BCrypt/Argon2).
- [ ] Реализовать `POST /auth/login` с выдачей JWT access/refresh токенов.
- [ ] Реализовать `POST /auth/refresh`.
- [ ] Публикация события `UserRegistered` через Outbox.
- [ ] Настроить EF Core миграции и Persistence для PostgreSQL (частично есть — проверить/дополнить).
- [ ] Unit-тесты Application, интеграционные тесты API (Testcontainers).
- [ ] Dockerfile + подключение в docker-compose.

### Фаза 2 — Clients Service (CRM контрагентов)
- [ ] Создать проект (Domain/Application/Persistence/Infrastructure/API).
- [ ] Сущности `Counterparty`, `ClientAccount`, миграции PostgreSQL.
- [ ] CRUD API + поиск по городу/названию/телефону.
- [ ] Consumer события `UserRegistered` → авто-создание `ClientAccount`.
- [ ] Тесты, Dockerfile, docker-compose.

### Фаза 3 — Pricing Service
- [ ] Создать проект по Clean Architecture.
- [ ] Сущность `TariffRate`, seed начальных тарифов (обычная/экспресс, деревянная/паллет/спец. упаковка, забор/доставка, страхование).
- [ ] Реализовать `POST /pricing/calculate` с бизнес-логикой расчёта (вес/объём/расстояние/услуги).
- [ ] Admin API для управления тарифами + публикация `TariffChanged`.
- [ ] Кэширование тарифов (in-memory/Redis) на стороне Orders Service — обновление по событию.
- [ ] Тесты расчёта (граничные случаи: нулевой вес, комбинации услуг), Dockerfile.

### Фаза 4 — Orders Service
- [ ] Создать проект по Clean Architecture.
- [ ] Сущность `Order` со всеми полями из ТЗ (см. 2.4), валидация (FluentValidation).
- [ ] Синхронный вызов `Pricing.calculate` при создании заявки (HttpClient + Polly retry/circuit breaker).
- [ ] `POST /orders`, подтверждение/отмена заявки, генерация номера заявки.
- [ ] Outbox + публикация `OrderCreated`/`OrderConfirmed`/`OrderCancelled`.
- [ ] Consumer `CargoStatusChanged`, `PaymentCompleted` — обновление статуса заявки в read-модели.
- [ ] Личный кабинет клиента: `GET /orders?clientId=` с фильтрами/пагинацией.
- [ ] Тесты, Dockerfile.

### Фаза 5 — Cargo Service (приёмка и трекинг)
- [ ] Создать проект по Clean Architecture.
- [ ] Сущности `Shipment`, `AcceptanceInspection`, `PackagingService`, `ShipmentStatusHistory`.
- [ ] Consumer `OrderConfirmed` → создание `Shipment` с генерацией трек-номера.
- [ ] API приёмки груза сотрудником: фиксация состояния упаковки/груза, фото (через File Storage), выполненные услуги упаковки.
- [ ] API изменения статуса + история статусов.
- [ ] Публичный `GET /track/{trackingNumber}` без авторизации (ограниченный набор полей).
- [ ] Публикация `CargoAccepted`, `CargoStatusChanged`, `CargoPhotoUploaded`, `CargoDelivered` (Outbox).
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
