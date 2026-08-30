using System.Text.Json;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using DocumentService.Application.Interfaces;
using DocumentService.Application.Models;
using DocumentService.Domain.Entities;
using DocumentService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application;

/// <summary>
/// Конвейер печати: документ из очереди → данные заявки → PDF → файл в хранилище → событие
/// <c>DocumentGenerated</c> через outbox.
/// </summary>
public class DocumentGenerationProcessor(
    IDocumentsRepository documentsRepository,
    IOrderSnapshotsRepository snapshotsRepository,
    IDocumentRenderer renderer,
    IFileStorageClient fileStorageClient,
    IOutboxWriter outboxWriter,
    ILogger<DocumentGenerationProcessor> logger) : IDocumentGenerationProcessor
{
    private const string PublishingService = "document-service";
    private const string PdfContentType = "application/pdf";

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var document = await documentsRepository.ClaimNextPendingAsync(cancellationToken);
        if (document is null)
            return false;

        logger.LogInformation(
            "Generating {DocumentType} for shipment {TrackingNumber} (document {DocumentId})",
            document.Type, document.TrackingNumber, document.Id);

        try
        {
            // Сведения о заявке может не быть: события груза и заявки идут разными очередями, и
            // ничто не обещает, что OrderCreated обработан раньше. Документ всё равно печатается —
            // с прочерками вместо неизвестных полей: бланк без номера заявки хуже, чем его
            // отсутствие, только если о нём никто не узнает, а так он хотя бы есть и виден.
            var snapshot = await snapshotsRepository.GetAsync(document.OrderId, cancellationToken);
            if (snapshot is null)
            {
                logger.LogWarning(
                    "No order snapshot for {OrderId}, printing {DocumentType} with blanks",
                    document.OrderId, document.Type);
            }

            var pdf = Render(document, snapshot);
            var fileId = await fileStorageClient.UploadAsync(pdf, PdfContentType, cancellationToken);

            // Событие ставится в outbox до сохранения: репозиторий делает SaveChanges на том же
            // DbContext, поэтому статус, ссылка на файл и событие коммитятся одной транзакцией.
            var generated = new DocumentGenerated
            {
                ShipmentId = document.ShipmentId,
                OrderId = document.OrderId,
                DocumentType = document.Type.ToString(),
                DocumentFileId = fileId,
            };

            outboxWriter.Enqueue(
                generated.EventId,
                RabbitMqConventions.RoutingKey(PublishingService, nameof(DocumentGenerated)),
                JsonSerializer.Serialize(generated),
                generated.OccurredAtUtc);

            await documentsRepository.MarkReadyAsync(document.Id, fileId, cancellationToken);

            logger.LogInformation(
                "Document {DocumentId} ({DocumentType}) stored as file {FileId}, {Size} bytes",
                document.Id, document.Type, fileId, pdf.Length);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Остановка сервиса: документ останется в Processing и будет виден как незаконченный.
            // Помечать его Failed нельзя — это не отказ, а прерывание.
            throw;
        }
        catch (Exception exception)
        {
            // Сюда попадают отказ хранилища, ошибка вёрстки, недоступная БД файлов. Документ
            // закрывается как Failed с причиной, а рабочий цикл продолжает разбирать очередь:
            // одна непечатаемая накладная не должна останавливать выпуск остальных.
            logger.LogError(exception, "Failed to generate document {DocumentId}", document.Id);
            await documentsRepository.MarkFailedAsync(document.Id, Truncate(exception.Message), CancellationToken.None);

            return true;
        }
    }

    private byte[] Render(Document document, OrderSnapshot? snapshot) => document.Type switch
    {
        DocumentType.Waybill => renderer.RenderWaybill(new WaybillModel
        {
            TrackingNumber = document.TrackingNumber,
            IssuedAt = document.IssuedAt,
            OrderNumber = snapshot?.OrderNumber,
            OriginCity = snapshot?.OriginCity,
            DestinationCity = snapshot?.DestinationCity,
            SenderName = snapshot?.SenderName,
            RecipientName = snapshot?.RecipientName,
            WeightKg = snapshot?.CargoWeightKg,
            VolumeM3 = snapshot?.CargoVolumeM3,
            DeclaredValue = snapshot?.DeclaredValue,
            Price = snapshot?.CalculatedPrice,
            DeliveryDeadline = snapshot?.DeliveryDeadline,
        }),

        DocumentType.AcceptanceAct => renderer.RenderAcceptanceAct(new AcceptanceActModel
        {
            TrackingNumber = document.TrackingNumber,
            HandedOverAt = document.IssuedAt,
            // Стадия выводится из данных самого документа: акт выдачи заводится с именем
            // получателя, акт приёмки — с состояниями груза.
            Stage = document.ReceivedByName is null && document.PackagingCondition is not null
                ? HandoverStage.Acceptance
                : HandoverStage.Delivery,
            OrderNumber = snapshot?.OrderNumber,
            PackagingCondition = document.PackagingCondition,
            CargoCondition = document.CargoCondition,
            ReceivedByName = document.ReceivedByName,
            SenderName = snapshot?.SenderName,
            RecipientName = snapshot?.RecipientName,
            CargoName = snapshot?.CargoName,
        }),

        DocumentType.DamageInspectionAct => renderer.RenderDamageInspectionAct(new DamageInspectionActModel
        {
            TrackingNumber = document.TrackingNumber,
            InspectedAt = document.IssuedAt,
            PackagingCondition = document.PackagingCondition ?? string.Empty,
            CargoCondition = document.CargoCondition ?? string.Empty,
            OrderNumber = snapshot?.OrderNumber,
            SenderName = snapshot?.SenderName,
            RecipientName = snapshot?.RecipientName,
            CargoName = snapshot?.CargoName,
            DeclaredValue = snapshot?.DeclaredValue,
        }),

        _ => throw new InvalidOperationException($"Неизвестный вид документа: {document.Type}."),
    };

    // Колонка FailureReason ограничена 1000 символами, а сообщения провайдеров бывают длиннее.
    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
