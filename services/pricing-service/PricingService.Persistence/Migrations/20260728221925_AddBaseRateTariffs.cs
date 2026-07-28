using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PricingService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseRateTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "tariff_rates",
                columns: new[] { "Id", "Category", "Code", "Name", "Price", "ValidFrom", "ValidTo" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000001"), "BaseRate", "PerKg", "Ставка за кг", 50m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("00000000-0000-0000-0005-000000000002"), "BaseRate", "PerKm", "Ставка за км", 15m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "tariff_rates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"));

            migrationBuilder.DeleteData(
                table: "tariff_rates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"));
        }
    }
}
