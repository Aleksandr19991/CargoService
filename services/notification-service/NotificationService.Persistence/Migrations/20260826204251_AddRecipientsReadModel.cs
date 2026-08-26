using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientsReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_recipients",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_recipients", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "order_recipients",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_recipients", x => x.OrderId);
                });

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                column: "Body",
                value: "Груз по заявке {{OrderNumber}} принят на склад.\n\nТрек-номер для отслеживания — {{TrackingNumber}}.\nСостояние упаковки при приёмке: {{PackagingCondition}}.\nСостояние груза: {{CargoCondition}}.");

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Оплата по заявке {{OrderNumber}} на сумму {{Amount}} руб. получена.\n\nСпасибо!", "Оплата по заявке {{OrderNumber}} получена" });

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Оплату по заявке {{OrderNumber}} провести не удалось.\n\nПричина: {{Reason}}.\nПовторите оплату в личном кабинете.", "Оплата по заявке {{OrderNumber}} не прошла" });

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"),
                column: "Body",
                value: "По заявке {{OrderNumber}} подготовлен документ: {{DocumentType}}.\n\nОн доступен в личном кабинете.");

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000010"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Автоматическая проверка фото выявила повреждение упаковки.\n\nГруз: {{ShipmentId}}.\nУверенность модели: {{Confidence}}.\nПроверка: {{InspectionJobId}}.\n\nТребуется перепроверка груза сотрудником склада.", "Автопроверка нашла повреждение упаковки, груз {{ShipmentId}}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_recipients");

            migrationBuilder.DropTable(
                name: "order_recipients");

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                column: "Body",
                value: "Груз принят на склад.\n\nТрек-номер для отслеживания — {{TrackingNumber}}.\nСостояние упаковки при приёмке: {{PackagingCondition}}.\nСостояние груза: {{CargoCondition}}.");

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Оплата на сумму {{Amount}} руб. получена.\n\nСпасибо!", "Оплата получена" });

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Оплату по заявке провести не удалось.\n\nПричина: {{Reason}}.\nПовторите оплату в личном кабинете.", "Оплата не прошла" });

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"),
                column: "Body",
                value: "По вашей заявке подготовлен документ: {{DocumentType}}.\n\nОн доступен в личном кабинете.");

            migrationBuilder.UpdateData(
                table: "notification_templates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000010"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Автоматическая проверка фото разошлась с оценкой сотрудника.\n\nГруз: {{ShipmentId}}.\nПовреждение по мнению модели: {{DamageDetected}} (уверенность {{Confidence}}).\nПроверка: {{InspectionJobId}}.\n\nТребуется перепроверка груза сотрудником склада.", "Расхождение оценки упаковки по грузу {{ShipmentId}}" });
        }
    }
}
