using Mapster;
using NotificationService.API.Models.Requests;
using NotificationService.API.Models.Responses;
using NotificationService.Domain.Entities;

namespace NotificationService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Body в ответ не идёт — см. докблок NotificationResponse; имена остальных полей
        // совпадают, поэтому правил не требуется.
        config.NewConfig<NotificationLog, NotificationResponse>();

        config.NewConfig<NotificationPreference, NotificationPreferencesResponse>();

        // UserId в запросе нет вовсе — его контроллер берёт из токена.
        config.NewConfig<UpdateNotificationPreferencesRequest, NotificationPreference>();
    }
}
