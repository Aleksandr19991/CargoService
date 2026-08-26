using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Application;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");

        builder.HasKey(template => template.Id);

        builder.Property(template => template.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(template => template.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(template => template.Subject)
            .HasMaxLength(200);

        // Без ограничения длины (Postgres text): тело письма пишет человек, и упереться в
        // произвольный лимит посреди правки шаблона он не должен.
        builder.Property(template => template.Body)
            .IsRequired();

        // Пара «повод + канал» и есть ключ поиска шаблона при отправке; уникальность не даёт
        // завести два конкурирующих текста на один и тот же случай — иначе выбор между ними
        // оказался бы делом порядка строк в таблице.
        builder.HasIndex(template => new { template.Code, template.Channel })
            .IsUnique();

        builder.HasData(SeedTemplates());
    }

    /// <summary>
    /// Стартовый набор шаблонов на все события, которые слушает сервис (spec.md §4).
    /// Email — на каждое событие: письмо ничего не стоит и лишним не бывает.
    /// SMS — только на три повода, требующих немедленной реакции или означающих конец пути
    /// (смена статуса, выдача, несостоявшаяся оплата): SMS платные и назойливые, слать ими
    /// каждое движение по заявке — верный способ добиться отписки от уведомлений вообще.
    /// Push-шаблонов нет: канал в enum есть, но отправителя для него в Фазе 6 не будет.
    /// </summary>
    private static NotificationTemplate[] SeedTemplates() =>
    [
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000001"),
            Code = NotificationTemplateCodes.OrderCreated,
            Channel = NotificationChannel.Email,
            Subject = "Заявка {{OrderNumber}} создана",
            Body = "Здравствуйте!\n\nВаша заявка {{OrderNumber}} на перевозку из города {{OriginCity}} в город {{DestinationCity}} создана.\nМы сообщим, когда она будет подтверждена.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000002"),
            Code = NotificationTemplateCodes.OrderConfirmed,
            Channel = NotificationChannel.Email,
            Subject = "Заявка {{OrderNumber}} подтверждена",
            Body = "Заявка {{OrderNumber}} подтверждена.\n\nСтоимость перевозки — {{CalculatedPrice}} руб.\nЖелаемый срок доставки — {{DeliveryDeadline}}.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000003"),
            Code = NotificationTemplateCodes.OrderCancelled,
            Channel = NotificationChannel.Email,
            Subject = "Заявка {{OrderNumber}} отменена",
            Body = "Заявка {{OrderNumber}} отменена.\n\nПричина: {{Reason}}",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000004"),
            Code = NotificationTemplateCodes.CargoAccepted,
            Channel = NotificationChannel.Email,
            Subject = "Груз принят на склад, трек-номер {{TrackingNumber}}",
            Body = "Груз по заявке {{OrderNumber}} принят на склад.\n\nТрек-номер для отслеживания — {{TrackingNumber}}.\nСостояние упаковки при приёмке: {{PackagingCondition}}.\nСостояние груза: {{CargoCondition}}.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000005"),
            Code = NotificationTemplateCodes.CargoStatusChanged,
            Channel = NotificationChannel.Email,
            Subject = "Груз {{TrackingNumber}}: статус «{{Status}}»",
            Body = "Статус груза {{TrackingNumber}} изменился на «{{Status}}».\n\nМестоположение: {{Location}}.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000006"),
            Code = NotificationTemplateCodes.CargoDelivered,
            Channel = NotificationChannel.Email,
            Subject = "Груз {{TrackingNumber}} выдан",
            Body = "Груз {{TrackingNumber}} выдан получателю {{ReceivedByName}}.\n\nСпасибо, что выбрали нас.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000007"),
            Code = NotificationTemplateCodes.PaymentCompleted,
            Channel = NotificationChannel.Email,
            Subject = "Оплата по заявке {{OrderNumber}} получена",
            Body = "Оплата по заявке {{OrderNumber}} на сумму {{Amount}} руб. получена.\n\nСпасибо!",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000008"),
            Code = NotificationTemplateCodes.PaymentFailed,
            Channel = NotificationChannel.Email,
            Subject = "Оплата по заявке {{OrderNumber}} не прошла",
            Body = "Оплату по заявке {{OrderNumber}} провести не удалось.\n\nПричина: {{Reason}}.\nПовторите оплату в личном кабинете.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000009"),
            Code = NotificationTemplateCodes.DocumentGenerated,
            Channel = NotificationChannel.Email,
            Subject = "Документ «{{DocumentType}}» готов",
            Body = "По заявке {{OrderNumber}} подготовлен документ: {{DocumentType}}.\n\nОн доступен в личном кабинете.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000010"),
            Code = NotificationTemplateCodes.PackageIntegrityAssessed,
            Channel = NotificationChannel.Email,
            Subject = "Автопроверка нашла повреждение упаковки, груз {{ShipmentId}}",
            // Про расхождение с оценкой сотрудника здесь не говорится: сравнивает оценки
            // cargo-service (он же ставит флаг), а это уведомление знает ровно то, что
            // приехало в событии от модели.
            Body = "Автоматическая проверка фото выявила повреждение упаковки.\n\nГруз: {{ShipmentId}}.\nУверенность модели: {{Confidence}}.\nПроверка: {{InspectionJobId}}.\n\nТребуется перепроверка груза сотрудником склада.",
        },

        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000001"),
            Code = NotificationTemplateCodes.CargoStatusChanged,
            Channel = NotificationChannel.Sms,
            Body = "Груз {{TrackingNumber}}: {{Status}}.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000002"),
            Code = NotificationTemplateCodes.CargoDelivered,
            Channel = NotificationChannel.Sms,
            Body = "Груз {{TrackingNumber}} выдан. Спасибо, что выбрали нас.",
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000003"),
            Code = NotificationTemplateCodes.PaymentFailed,
            Channel = NotificationChannel.Sms,
            Body = "Оплата по заявке не прошла. Повторите оплату в личном кабинете.",
        },
    ];
}
