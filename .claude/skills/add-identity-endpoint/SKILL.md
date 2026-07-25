---
name: add-identity-endpoint
description: Scaffolds a new HTTP endpoint in services/identity-service/IdentityService (the API project) — controller action in Controllers/, request/response DTOs as records, a FluentValidation validator, and a Mapster mapping entry — following this repo's established conventions. Use whenever asked to add, expose, or wire up a new endpoint/action/route on IdentityService's API, or on any future service's API project once it adopts the same conventions (see CLAUDE.md).
---

# Добавление эндпоинта в API identity-service

Этот скилл описывает обязательный шаблон для любого нового HTTP-эндпоинта в `services/identity-service/IdentityService/` (проект API-хоста). Шаблон закреплён в CLAUDE.md как конвенция — не изобретай другой способ мапить/валидировать/объявлять DTO, даже если он кажется проще для конкретного случая.

Четыре правила, из которых состоит шаблон:

1. **Контроллер — только в `Controllers/`**, namespace `IdentityService.API.Controllers`.
2. **Request/Response DTO — только `sealed record` с `required init`-свойствами**, не класс и не позиционный record. DTO — это неизменяемый слепок данных на один проход туда/обратно, а не объект с поведением; `required init` явно требует заполнить все поля при создании и убирает предупреждения компилятора о nullable-свойствах без значения.
3. **Маппинг — только через Mapster** (`IMapper`, внедряется в конструктор). Ноль ручных `new SomeDto { ... }` в контроллере.
4. **Валидация — только через FluentValidation** (`AbstractValidator<TRequest>` в `Validators/`). Ноль ручных проверок `if (string.IsNullOrEmpty(...))` в контроллере — `ValidationFilter` делает это глобально до входа в тело метода.

## Чек-лист

Для нового эндпоинта, у которого есть тело запроса и/или собственный ответ:

- [ ] **Request DTO** — `Models/Requests/{Action}Request.cs`, `sealed record` с `required` свойствами `{ get; init; }` (без логики).
- [ ] **Response DTO** (если ответ не переиспользует существующий) — `Models/Responses/{Action}Response.cs`, тот же стиль.
- [ ] **Validator** — `Validators/{Action}RequestValidator.cs`, `AbstractValidator<{Action}Request>` с правилами `RuleFor(...)`.
- [ ] **Запись в `Mapping/MappingRegister.cs`** — `config.NewConfig<{Source}, {Destination}>();` для каждого направления маппинга, которое использует эндпоинт (DTO → сущность и/или сущность → DTO).
- [ ] **Метод действия** в существующем или новом контроллере под `Controllers/`, использующий внедрённый `IMapper` — без ручного маппинга и без ручной валидации.
- [ ] Если нужна новая бизнес-логика — она идёт в `IdentityService.Application` (сервис/интерфейс), контроллер её не реализует, только вызывает.

## Референсные файлы (читать перед тем, как писать новый код)

- `services/identity-service/IdentityService/Models/Requests/RegisterUserRequest.cs` — эталонный request DTO: `sealed record`, все свойства `required ... { get; init; }`.
- `services/identity-service/IdentityService/Controllers/UsersController.cs` — эталонный контроллер: `[Authorize]` на классе по умолчанию, `[AllowAnonymous]`/`[Authorize(Roles = ...)]` точечно на действиях, внедрённые `IUsersService` + `IMapper`, ни одной ручной проверки или маппинга в теле методов.
- `services/identity-service/IdentityService/Mapping/MappingRegister.cs` — все пары маппинга сервиса, одним файлом.
- `services/identity-service/IdentityService/Filters/ValidationFilter.cs` — как работает автоматическая валидация (для справки, обычно не требует изменений).
- `services/identity-service/IdentityService/Validators/RegisterUserRequestValidator.cs` — эталонный validator.
- `services/identity-service/IdentityService/Configuration/ServicesConfiguration.cs` — `AddApiServices()`, где зарегистрированы `AddMapster()`, `AddValidatorsFromAssemblyContaining<Program>()` и `options.Filters.AddValidationFilter()` (трогать не нужно, если это не первый эндпоинт в новом сервисе). `Program.cs` в identity-service держится коротким и просто вызывает такие extension-методы — не добавляй регистрации инлайном туда, заводи/дополняй extension-метод в `Configuration/`.

## Шаблоны

### Request DTO

```csharp
namespace IdentityService.API.Models.Requests;

public sealed record {Action}Request
{
    public required string SomeField { get; init; }
}
```

### Response DTO

```csharp
namespace IdentityService.API.Models.Responses;

public sealed record {Action}Response
{
    public required Guid Id { get; init; }
}
```

### Validator

```csharp
using FluentValidation;
using IdentityService.API.Models.Requests;

namespace IdentityService.API.Validators;

public class {Action}RequestValidator : AbstractValidator<{Action}Request>
{
    public {Action}RequestValidator()
    {
        RuleFor(x => x.SomeField).NotEmpty().MaximumLength(100);
        // .EmailAddress() для email-полей, .IsInEnum() для enum-полей, .MinimumLength(n) для паролей — см. существующие валидаторы.
    }
}
```

### Запись в MappingRegister

```csharp
// В IdentityService.API.Mapping.MappingRegister.Register(...)
config.NewConfig<{Action}Request, User>();   // DTO → сущность, если эндпоинт что-то создаёт/меняет
config.NewConfig<User, {Action}Response>();  // сущность → DTO, если эндпоинт что-то возвращает
```

Если у DTO есть свойство без пары на другой стороне (например, `Password` есть у request, но не хранится на сущности) — ничего дополнительно настраивать не нужно, Mapster просто игнорирует несовпадающие свойства и корректно заполняет `required`-свойства назначения через object-initializer. Кастомная трансформация значения (не 1:1 копирование) добавляется через `.Map(dest => dest.X, src => src.Y)` в цепочке `NewConfig<>()` — см. документацию Mapster, если такое понадобится.

### Метод контроллера

```csharp
[HttpPost("{route}")]
[Authorize(Roles = nameof(Role.Admin))] // или [AllowAnonymous] — по смыслу эндпоинта
public async Task<ActionResult<{Action}Response>> {Action}(
    [FromBody] {Action}Request request,
    CancellationToken cancellationToken)
{
    var entity = mapper.Map<User>(request);
    var result = await someService.DoSomethingAsync(entity, cancellationToken);
    return Ok(mapper.Map<{Action}Response>(result));
}
```

## Что этот скилл не делает

Не создаёт бизнес-логику в `IdentityService.Application`/`IdentityService.Persistence` — если новому эндпоинту нужен новый метод сервиса, новый метод репозитория или новая миграция EF Core, это отдельная задача уровня Application/Persistence, а не API-слоя. Этот скилл покрывает только контроллер + DTO + валидацию + маппинг вокруг уже существующей или тривиально добавляемой бизнес-операции.

Если делаешь такой же эндпоинт в другом сервисе (не identity-service), который ещё не завёл `Mapping/`, `Validators/`, `Filters/ValidationFilter.cs` и свой `Configuration/ServicesConfiguration.cs` с `AddApiServices()` — сначала перенеси туда эту инфраструктуру (скопировав из identity-service), а не изобретай альтернативный способ мапить/валидировать/объявлять DTO для одного сервиса.
