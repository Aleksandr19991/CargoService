# document-service

Генерация документов по грузу: транспортная накладная, акт приёма-передачи, акт осмотра при
повреждении, QR/штрихкод трек-номера. См. раздел [3.1 spec.md](../../spec.md#31-document-service--генерация-документов).

Реализуется в рамках Фазы 8 (см. чек-лист в spec.md).

Пять слоёв по общему образцу монорепозитория: `DocumentService.Domain` / `.Application` /
`.Persistence` / `.Infrastructure` / `DocumentService` (API-хост). БД и пользователь Postgres —
`documentservice`, порт для будущей записи в `docker-compose.yml` зарезервирован **8089**.

Сервис одновременно потребитель и издатель событий: подписывается на `CargoAccepted` и
`CargoDelivered`, публикует `DocumentGenerated`, — поэтому у него будут и consumer с inbox, и
транзакционный outbox (как у ai-inspection-service). Сами PDF хранятся в file-storage-service,
у себя сервис держит только метаданные документа и идентификатор файла.

## Команды

```
dotnet build DocumentService.slnx
dotnet run --project DocumentService
```

Миграции (из `DocumentService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
