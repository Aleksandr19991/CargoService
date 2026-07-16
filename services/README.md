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
└── ai-inspection-service/
```

Список соответствует разделу 2 spec.md. Дополнительные сервисы из раздела 3 (document-service, payment-service, logistics-service, file-storage-service, reporting-service) добавляются сюда по мере реализации соответствующих фаз.

Внутри папки сервиса — проекты по слоям Clean Architecture (`{ServiceName}.Domain`, `.Application`, `.Persistence`, `.Infrastructure`, `.API`), см. раздел 1 [spec.md](../spec.md).

Общие для всех сервисов DTO событий RabbitMQ находятся в [`shared/CargoService.Contracts`](../shared/CargoService.Contracts), а не внутри отдельного сервиса.
