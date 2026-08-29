namespace AiInspectionService.Domain.Enums;

/// <summary>Жизненный цикл задания на проверку (spec.md §2.7).</summary>
public enum InspectionJobStatus
{
    /// <summary>Задание принято и ждёт инференса.</summary>
    Queued,

    /// <summary>Инференс идёт: строка занята обработчиком, повторно её брать нельзя.</summary>
    Processing,

    /// <summary>Все снимки обработаны, результаты записаны.</summary>
    Completed,

    /// <summary>Обработка не удалась (файл не скачался, модель отказала) — причина в FailureReason.</summary>
    Failed
}
