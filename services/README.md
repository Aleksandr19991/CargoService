# services

Каждый микросервис системы располагается в отдельной папке этого каталога:

```
services/
├── identity-service/
├── clients-service/
├── pricing-service/
├── orders-service/
├── cargo-service/
├── notification-service/
├── ai-inspection-service/
├── file-storage-service/
├── document-service/
└── payment-service/
```

Первые семь папок соответствуют разделу 2 spec.md, остальные — дополнительные сервисы из раздела 3; они добавляются сюда по мере реализации соответствующих фаз (ещё не созданы: logistics-service, reporting-service, api-gateway).

Внутри папки сервиса — проекты по слоям Clean Architecture (`{ServiceName}.Domain`, `.Application`, `.Persistence`, `.Infrastructure`, `.API`), см. раздел 1 [spec.md](../spec.md).

Общие для всех сервисов DTO событий RabbitMQ находятся в [`shared/CargoService.Contracts`](../shared/CargoService.Contracts), а не внутри отдельного сервиса.
