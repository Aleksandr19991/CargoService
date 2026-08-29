using AiInspectionService.Application.Models;

namespace AiInspectionService.Application.Interfaces;

/// <summary>
/// Модель компьютерного зрения, оценивающая целостность упаковки по снимку.
/// <para>
/// Интерфейс живёт в Application, реализации — в Infrastructure: сегодня это ONNX-модель в
/// процессе сервиса, завтра может быть внешний CV API, и остальному коду разница не видна.
/// Байты снимка передаются как есть (JPEG/PNG) — препроцессинг под конкретную модель её же
/// и дело, а не вызывающего.
/// </para>
/// </summary>
public interface IPackageInspectionModel
{
    /// <summary>Версия, которой подписываются вердикты (см. <see cref="PackageInspectionVerdict.ModelVersion"/>).</summary>
    string Version { get; }

    Task<PackageInspectionVerdict> InspectAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
