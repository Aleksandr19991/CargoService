using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveryDeadline",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_DeliveryDeadline_CurrentStatus",
                table: "shipments",
                columns: new[] { "DeliveryDeadline", "CurrentStatus" },
                filter: "\"DeliveryDeadline\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipments_DeliveryDeadline_CurrentStatus",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryDeadline",
                table: "shipments");
        }
    }
}
