using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrdersService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SenderCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SenderIsOrganization = table.Column<bool>(type: "boolean", nullable: false),
                    SenderPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecipientCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientIsOrganization = table.Column<bool>(type: "boolean", nullable: false),
                    RecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DestinationCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CargoName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CargoWeight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CargoQuantity = table.Column<int>(type: "integer", nullable: false),
                    DeclaredValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RequestedShipDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShippingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PackagingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NeedsPickup = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsInsurance = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderCounterpartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientCounterpartyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ClientAccountId",
                table: "orders",
                column: "ClientAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
