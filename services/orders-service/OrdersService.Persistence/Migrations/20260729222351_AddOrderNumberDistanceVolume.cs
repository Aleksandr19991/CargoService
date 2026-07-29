using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrdersService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberDistanceVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CargoVolumeM3",
                table: "orders",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceKm",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_orders_Number",
                table: "orders",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_Number",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CargoVolumeM3",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "orders");
        }
    }
}
