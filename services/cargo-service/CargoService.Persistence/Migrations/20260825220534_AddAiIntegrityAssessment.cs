using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiIntegrityAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiAssessedAt",
                table: "acceptance_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                table: "acceptance_inspections",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiDamageDetected",
                table: "acceptance_inspections",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiInspectionJobId",
                table: "acceptance_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAssessmentDiscrepancy",
                table: "acceptance_inspections",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_inspections_HasAssessmentDiscrepancy",
                table: "acceptance_inspections",
                column: "HasAssessmentDiscrepancy",
                filter: "\"HasAssessmentDiscrepancy\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_acceptance_inspections_HasAssessmentDiscrepancy",
                table: "acceptance_inspections");

            migrationBuilder.DropColumn(
                name: "AiAssessedAt",
                table: "acceptance_inspections");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "acceptance_inspections");

            migrationBuilder.DropColumn(
                name: "AiDamageDetected",
                table: "acceptance_inspections");

            migrationBuilder.DropColumn(
                name: "AiInspectionJobId",
                table: "acceptance_inspections");

            migrationBuilder.DropColumn(
                name: "HasAssessmentDiscrepancy",
                table: "acceptance_inspections");
        }
    }
}
