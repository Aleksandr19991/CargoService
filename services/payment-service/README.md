# payment-service

Оплата услуг по заявке: приём платежа (эквайринг/платёжная ссылка), статус оплаты, возвраты при
отмене заявки. См. раздел [3.2 spec.md](../../spec.md#32-payment-service--оплата-услуг).

Реализуется в рамках Фазы 9 (см. чек-лист в spec.md).

Пять слоёв по общему образцу монорепозитория: `PaymentService.Domain` / `.Application` /
`.Persistence` / `.Infrastructure` / `PaymentService` (API-хост). БД и пользователь Postgres —
`paymentservice`, порт для будущей записи в `docker-compose.yml` зарезервирован **8090**.

Сервис одновременно потребитель и издатель событий: подписывается на `OrderConfirmed` (выставление
счёта) и `OrderCancelled` (возврат), публикует `PaymentCompleted`, `PaymentFailed`, `RefundIssued`, —
поэтому у него будут и consumer с inbox, и транзакционный outbox (как у document-service).

Деньгами распоряжается внешний платёжный провайдер; у себя сервис хранит счета, платежи и их
статусы, а также идентификаторы транзакций провайдера — по ним сопоставляются входящие webhook.

## Команды

```
dotnet build PaymentService.slnx
dotnet run --project PaymentService
```

Миграции (из `PaymentService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
