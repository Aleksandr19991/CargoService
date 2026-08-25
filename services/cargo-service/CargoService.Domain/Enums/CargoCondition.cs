namespace CargoService.Domain.Enums;

/// <summary>
/// Состояние самого груза на приёмке (ТЗ: Целый/Повреждён). Намеренно отдельный enum от
/// <see cref="PackagingCondition"/>, хотя набор значений сегодня совпадает: это разные предметы
/// оценки, и расширяться они будут независимо (упаковка может стать «Отсутствует», груз —
/// «Повреждён частично»).
/// </summary>
public enum CargoCondition
{
    Intact,
    Damaged
}
