using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "notification_templates",
                columns: new[] { "Id", "Body", "Channel", "Code", "Subject" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "Здравствуйте!\n\nВаша заявка {{OrderNumber}} на перевозку из города {{OriginCity}} в город {{DestinationCity}} создана.\nМы сообщим, когда она будет подтверждена.", "Email", "OrderCreated", "Заявка {{OrderNumber}} создана" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "Заявка {{OrderNumber}} подтверждена.\n\nСтоимость перевозки — {{CalculatedPrice}} руб.\nЖелаемый срок доставки — {{DeliveryDeadline}}.", "Email", "OrderConfirmed", "Заявка {{OrderNumber}} подтверждена" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "Заявка {{OrderNumber}} отменена.\n\nПричина: {{Reason}}", "Email", "OrderCancelled", "Заявка {{OrderNumber}} отменена" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "Груз принят на склад.\n\nТрек-номер для отслеживания — {{TrackingNumber}}.\nСостояние упаковки при приёмке: {{PackagingCondition}}.\nСостояние груза: {{CargoCondition}}.", "Email", "CargoAccepted", "Груз принят на склад, трек-номер {{TrackingNumber}}" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "Статус груза {{TrackingNumber}} изменился на «{{Status}}».\n\nМестоположение: {{Location}}.", "Email", "CargoStatusChanged", "Груз {{TrackingNumber}}: статус «{{Status}}»" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "Груз {{TrackingNumber}} выдан получателю {{ReceivedByName}}.\n\nСпасибо, что выбрали нас.", "Email", "CargoDelivered", "Груз {{TrackingNumber}} выдан" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "Оплата на сумму {{Amount}} руб. получена.\n\nСпасибо!", "Email", "PaymentCompleted", "Оплата получена" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), "Оплату по заявке провести не удалось.\n\nПричина: {{Reason}}.\nПовторите оплату в личном кабинете.", "Email", "PaymentFailed", "Оплата не прошла" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), "По вашей заявке подготовлен документ: {{DocumentType}}.\n\nОн доступен в личном кабинете.", "Email", "DocumentGenerated", "Документ «{{DocumentType}}» готов" },
                    { new Guid("00000000-0000-0000-0001-000000000010"), "Автоматическая проверка фото разошлась с оценкой сотрудника.\n\nГруз: {{ShipmentId}}.\nПовреждение по мнению модели: {{DamageDetected}} (уверенность {{Confidence}}).\nПроверка: {{InspectionJobId}}.\n\nТребуется перепроверка груза сотрудником склада.", "Email", "PackageIntegrityAssessed", "Расхождение оценки упаковки по грузу {{ShipmentId}}" },
                    { new Guid("00000000-0000-0000-0002-000000000001"), "Груз {{TrackingNumber}}: {{Status}}.", "Sms", "CargoStatusChanged", null },
                    { new Guid("00000000-0000-0000-0002-000000000002"), "Груз {{TrackingNumber}} выдан. Спасибо, что выбрали нас.", "Sms", "CargoDelivered", null },
                    { new Guid("00000000-0000-0000-0002-000000000003"), "Оплата по заявке не прошла. Повторите оплату в личном кабинете.", "Sms", "PaymentFailed", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_RecipientUserId_CreatedAt",
                table: "notification_logs",
                columns: new[] { "RecipientUserId", "CreatedAt" },
                filter: "\"RecipientUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_Code_Channel",
                table: "notification_templates",
                columns: new[] { "Code", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_logs");

            migrationBuilder.DropTable(
                name: "notification_templates");
        }
    }
}
