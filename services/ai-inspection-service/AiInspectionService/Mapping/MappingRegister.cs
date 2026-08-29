using AiInspectionService.API.Models.Responses;
using AiInspectionService.Application.Models;
using AiInspectionService.Domain.Entities;
using Mapster;

namespace AiInspectionService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // RawResponse в ответ не идёт — см. докблок InspectionResultResponse; остальные имена
        // совпадают, поэтому правил не требуется.
        config.NewConfig<InspectionResult, InspectionResultResponse>();

        // Вердикты отдаются в порядке проверки снимков — иначе он зависел бы от того, как
        // строки легли в таблицу.
        config.NewConfig<InspectionJob, InspectionJobResponse>()
            .Map(dest => dest.Results, src => src.Results.OrderBy(result => result.AssessedAt));

        // CreateInspectionRequest в сущность не маппится: задание собирает Application-сервис,
        // проставляя статус и время, — контроллер передаёт ему только поля запроса.

        // Precision/Recall/F1/Accuracy — вычисляемые свойства матрицы, Mapster переносит их
        // как обычные значения; CurrentThreshold в отчёте нет — его добавляет контроллер.
        config.NewConfig<ConfusionMatrix, ConfusionMatrixResponse>();
        config.NewConfig<ModelEvaluationReport, ModelEvaluationResponse>()
            .Ignore(dest => dest.CurrentThreshold);
    }
}
