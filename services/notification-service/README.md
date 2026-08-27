# notification-service

Уведомления клиента о статусе груза. См. раздел [2.6 spec.md](../../spec.md#26-notification-service--уведомления-о-статусе-груза).

Реализован в рамках Фазы 6 (см. чек-лист в spec.md).

Пять слоёв по общему образцу монорепозитория: `NotificationService.Domain` / `.Application` /
`.Persistence` / `.Infrastructure` / `NotificationService` (API-хост). БД и пользователь Postgres —
`notificationservice`, в `docker-compose.yml` сервис называется `notificationservice` и слушает
порт **8087**.

Собственных событий сервис не публикует — он конечный потребитель статусных событий, поэтому
outbox здесь нет; вместо него от повторных доставок защищает inbox (`inbox_messages`, ключ —
`EventId` события).

## API

Всё под ролью `Client`, получатель берётся из claim `sub` токена (параметра `clientId` нет):

- `GET /api/notifications` — своя история (фильтр `channel`, пагинация `page`/`pageSize`);
- `GET /api/notifications/preferences` — свои настройки каналов;
- `POST /api/notifications/preferences` — сохранить настройки целиком (`emailEnabled`, `smsEnabled`).

Если настройки никогда не сохранялись, включены все каналы, для которых есть шаблон и контакт.

## Откуда сервис знает, кому слать

Статусные события несут только `OrderId`, контактов клиента в них нет. Поэтому сервис держит
две read-модели: `notification_recipients` (контакты, наполняется событием `UserRegistered`) и
`order_recipients` (заявка → владелец и номер заявки, наполняется `OrderCreated`). Если события
`OrderCreated` по заявке сервис не видел, уведомления по ней пропускаются с предупреждением в
логе — синхронно спрашивать контакты в чужих сервисах он не ходит.

## Каналы отправки

Email уходит по SMTP (секция конфигурации `Smtp`). Локально почту принимает **Mailpit** из
`docker-compose.yml`: SMTP на `localhost:1025`, письма видны в UI на http://localhost:8025 —
наружу ничего не отправляется. В проде те же настройки указывают на провайдера.

SMS отправляются через REST API провайдера (секция `Sms`, контракт Twilio). Пока `AccountSid`/
`AuthToken`/`FromNumber` не заданы, вместо реального отправителя регистрируется
`LoggingSmsSender`: сообщение пишется в лог предупреждением и считается отправленным.

## Команды

```
dotnet build NotificationService.slnx
dotnet run --project NotificationService
```

Тесты:

```
dotnet test NotificationService.Application.Tests   # без внешних зависимостей
dotnet test NotificationService.IntegrationTests    # нужен запущенный Docker-демон (Testcontainers)
```

Миграции (из `NotificationService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
