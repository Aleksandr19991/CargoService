using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiInspectionService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inspection_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoFileIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inspection_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagingIntegrityScore = table.Column<double>(type: "double precision", nullable: false),
                    DamageDetected = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    DamageTypes = table.Column<List<string>>(type: "text[]", nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RawResponse = table.Column<string>(type: "text", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inspection_results_inspection_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "inspection_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inspection_jobs_ShipmentId_CreatedAt",
                table: "inspection_jobs",
                columns: new[] { "ShipmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inspection_jobs_Status",
                table: "inspection_jobs",
                column: "Status",
                filter: "\"Status\" = 'Queued'");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_results_JobId_FileId",
                table: "inspection_results",
                columns: new[] { "JobId", "FileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inspection_results");

            migrationBuilder.DropTable(
                name: "inspection_jobs");
        }
    }
}
