using DocumentService.Application.Models;

namespace DocumentService.Application.Interfaces;

/// <summary>
/// Вёрстка документов в PDF. Интерфейс живёт в Application, реализация — в Infrastructure:
/// вёрстка это внешняя подробность (сегодня QuestPDF, завтра шаблонизатор или сторонний
/// сервис печати), а сценарии (кто и по какому событию печатает документ) от неё не зависят и
/// тестируются без неё.
/// </summary>
public interface IDocumentRenderer
{
    byte[] RenderWaybill(WaybillModel model);

    byte[] RenderAcceptanceAct(AcceptanceActModel model);

    byte[] RenderDamageInspectionAct(DamageInspectionActModel model);
}
