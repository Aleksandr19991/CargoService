# ai-inspection-service

Визуальная оценка целостности упаковки через ИИ. См. раздел [2.7 spec.md](../../spec.md#27-ai-inspection-service--визуальная-оценка-целостности-упаковки).

Реализуется в рамках Фазы 7 (см. чек-лист в spec.md).

Пять слоёв по общему образцу монорепозитория: `AiInspectionService.Domain` / `.Application` /
`.Persistence` / `.Infrastructure` / `AiInspectionService` (API-хост). БД и пользователь Postgres —
`aiinspectionservice`, порт для будущей записи в `docker-compose.yml` зарезервирован **8088**.

Сокращение «AI» в именах проектов и namespace записано как `Ai`, а не `AI`: так уже написаны
поля `AiDamageDetected`/`AiConfidence` в cargo-service, и разнобой в одном репозитории хуже, чем
отступление от буквы рекомендаций по именованию.

Сервис — потребитель события `CargoPhotoUploaded` и издатель `PackageIntegrityAssessed`, поэтому
у него будут и consumer, и транзакционный outbox (в отличие от notification-service, который
ничего не публикует).

## Команды

```
dotnet build AiInspectionService.slnx
dotnet run --project AiInspectionService
```

Миграции (из `AiInspectionService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
