using System.Security.Cryptography;
using CargoService.Application.Interfaces;
using CargoService.Domain.Entities;
using CargoService.Domain.Enums;

namespace CargoService.Application;

public class ShipmentsService(IShipmentsRepository shipmentsRepository) : IShipmentsService
{
    // Тот же алфавит без 0/O/1/I, что у номера заявки в orders-service: трек-номер клиенты
    // диктуют по телефону и вбивают руками, визуально неоднозначные символы здесь дороже всего.
    private const string TrackingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TrackingSuffixLength = 10;
    private const string TrackingPrefix = "CS-";

    public async Task<Shipment?> CreateFromConfirmedOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Обычную повторную доставку события отсекаем здесь — дёшево и, главное, без исключения
        // на стороне EF (см. комментарий в ShipmentsRepository.TryCreateAsync про шум в логах).
        // Гонку двух одновременных доставок эта проверка не закрывает — для неё есть индекс.
        if (await shipmentsRepository.ExistsByOrderIdAsync(orderId, cancellationToken))
            return null;

        var now = DateTimeOffset.UtcNow;
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TrackingNumber = GenerateTrackingNumber(),
            CurrentStatus = ShipmentStatus.Created,
            CreatedAt = now,
        };

        // Первая запись истории заводится сразу, чтобы трекинг с самого начала показывал
        // хронологию, а не пустой список рядом с уже выставленным CurrentStatus.
        shipment.StatusHistory.Add(new ShipmentStatusHistory
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.Created,
            ChangedAt = now,
        });

        return await shipmentsRepository.TryCreateAsync(shipment, cancellationToken);
    }

    /// <summary>
    /// Формат <c>CS-XXXXXXXXXX</c>. Намеренно отличается от номера заявки
    /// (<c>{yyyyMMdd}-{6 символов}</c> в orders-service): трек-номер живёт на публичном
    /// эндпоинте, дата в нём лишняя, а префикс не даёт спутать его с номером заявки.
    /// 32^10 комбинаций — коллизия практически невероятна, страхует уникальный индекс в БД.
    /// </summary>
    private static string GenerateTrackingNumber()
    {
        Span<char> suffix = stackalloc char[TrackingSuffixLength];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = TrackingAlphabet[RandomNumberGenerator.GetInt32(TrackingAlphabet.Length)];

        return TrackingPrefix + new string(suffix);
    }
}
