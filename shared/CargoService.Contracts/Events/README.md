# Events

Версионированные DTO событий, публикуемых через RabbitMQ (см. раздел 4 "Контракты событий RabbitMQ" в [spec.md](../../../spec.md)).

Правила:
- Один файл — одно событие, имя файла совпадает с именем события (например, `V1/OrderCreated.cs`).
- Все события версии 1 лежат в `Events/V1/` с неймспейсом `CargoService.Contracts.Events.V1` и наследуются от `IntegrationEvent` (`EventId`, `OccurredAtUtc`).
- Только данные (record/DTO с `required`/nullable-свойствами), без бизнес-логики и зависимостей на конкретные сервисы.
- При breaking change — новая папка/неймспейс `V2`, не правится опубликованный контракт задним числом (аддитивные изменения полей можно вносить в V1 без версионирования).

## Соглашения об именовании RabbitMQ

Реализовано в [`Messaging/RabbitMqConventions.cs`](../Messaging/RabbitMqConventions.cs) — не пересчитывать вручную, использовать эти методы.

- **Exchange**: один топик-exchange на всю систему — `cargoservice.events` (`RabbitMqConventions.EventsExchange`), тип `topic`.
- **Routing key**: `RabbitMqConventions.RoutingKey(publishingService, nameof(EventType))` → `{publishing-service}.{event-name-in-kebab-case}`, например `orders-service.order-created`, `cargo-service.cargo-status-changed`. `{publishing-service}` — имя папки сервиса из `services/` (без суффикса `-service` не сокращаем, чтобы routing key был однозначным).
- **Очередь потребителя**: `RabbitMqConventions.QueueName(consumingService, nameof(EventType))` → `{consuming-service}.{event-name-in-kebab-case}`, например `notification-service.order-created`. У каждого потребителя своя очередь, забинженная на нужный routing key — sharing очередей между сервисами не допускается.
- **Dead-letter очередь**: `{имя очереди}` + суффикс `.dlq` (`RabbitMqConventions.DeadLetterSuffix`), например `notification-service.order-created.dlq`.
- Пример реального использования — публикация `UserRegistered` в identity-service через паттерн Outbox: `services/identity-service/IdentityService.Infrastructure/Outbox/OutboxDispatcher.cs`.
