# notification-service

Уведомления клиента о статусе груза. См. раздел [2.6 spec.md](../../spec.md#26-notification-service--уведомления-о-статусе-груза).

Реализуется в рамках Фазы 6 (см. чек-лист в spec.md).

Пять слоёв по общему образцу монорепозитория: `NotificationService.Domain` / `.Application` /
`.Persistence` / `.Infrastructure` / `NotificationService` (API-хост). БД и пользователь Postgres —
`notificationservice`, порт для будущей записи в `docker-compose.yml` зарезервирован **8087**.

Собственных событий сервис публиковать не планирует — он конечный потребитель статусных событий,
поэтому outbox здесь не появится; вместо него в Фазе 6 будет inbox по `MessageId` для
идемпотентности обработки.

## Команды

```
dotnet build NotificationService.slnx
dotnet run --project NotificationService
```

Миграции (из `NotificationService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
