using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FEENALOoFINALE.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturerDocumentsAndRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManufacturerDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturerDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_ManufacturerDocuments_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "EquipmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecommendations",
                columns: table => new
                {
                    RecommendationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    RecommendationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecommendationText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAppliedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecommendations", x => x.RecommendationId);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecommendations_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "EquipmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecommendations_ManufacturerDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ManufacturerDocuments",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecommendations_DocumentId",
                table: "MaintenanceRecommendations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecommendations_EquipmentId",
                table: "MaintenanceRecommendations",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturerDocuments_EquipmentId",
                table: "ManufacturerDocuments",
                column: "EquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceRecommendations");

            migrationBuilder.DropTable(
                name: "ManufacturerDocuments");
        }
    }
}
