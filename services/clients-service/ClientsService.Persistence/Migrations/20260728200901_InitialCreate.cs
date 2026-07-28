using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientsService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "counterparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Inn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_counterparties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_counterparties_client_accounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalTable: "client_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_accounts_UserId",
                table: "client_accounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_City",
                table: "counterparties",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_ClientAccountId",
                table: "counterparties",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_FullName",
                table: "counterparties",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_OrganizationName",
                table: "counterparties",
                column: "OrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_Phone",
                table: "counterparties",
                column: "Phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "counterparties");

            migrationBuilder.DropTable(
                name: "client_accounts");
        }
    }
}
