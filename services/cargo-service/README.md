# cargo-service

Приёмка, обработка и трекинг груза. См. раздел [2.5 spec.md](../../spec.md#25-cargo-service--приёмка-обработка-и-трекинг-груза).

Реализуется в рамках Фазы 5 (см. чек-лист в spec.md).

## Особенность именования

БД, пользователь Postgres и compose-сервис называются **`cargoshipments`**, а не `cargoservice`
по общей конвенции `{сервис}service` — имя `cargoservice` занято **identity-service** (наследие
названия платформы, появившегося до этого сервиса). Проекты и namespace при этом обычные:
`CargoService.Domain`, `CargoService.Application` и т.д.

## Команды

```
dotnet build CargoService.slnx
dotnet run --project CargoService
```

Миграции (из `CargoService.Persistence/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```
