using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FEENALOoFINALE.Migrations
{
    /// <inheritdoc />
    public partial class ShareDocumentsAcrossEquipmentModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecommendations_Equipment_EquipmentId",
                table: "MaintenanceRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ManufacturerDocuments_Equipment_EquipmentId",
                table: "ManufacturerDocuments");

            migrationBuilder.RenameColumn(
                name: "EquipmentId",
                table: "ManufacturerDocuments",
                newName: "EquipmentModelId");

            migrationBuilder.RenameIndex(
                name: "IX_ManufacturerDocuments_EquipmentId",
                table: "ManufacturerDocuments",
                newName: "IX_ManufacturerDocuments_EquipmentModelId");

            migrationBuilder.AddColumn<int>(
                name: "UploadedByEquipmentId",
                table: "ManufacturerDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EquipmentId",
                table: "MaintenanceRecommendations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "EquipmentModelId",
                table: "MaintenanceRecommendations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturerDocuments_UploadedByEquipmentId",
                table: "ManufacturerDocuments",
                column: "UploadedByEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecommendations_EquipmentModelId",
                table: "MaintenanceRecommendations",
                column: "EquipmentModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecommendations_EquipmentModels_EquipmentModelId",
                table: "MaintenanceRecommendations",
                column: "EquipmentModelId",
                principalTable: "EquipmentModels",
                principalColumn: "EquipmentModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecommendations_Equipment_EquipmentId",
                table: "MaintenanceRecommendations",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "EquipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ManufacturerDocuments_EquipmentModels_EquipmentModelId",
                table: "ManufacturerDocuments",
                column: "EquipmentModelId",
                principalTable: "EquipmentModels",
                principalColumn: "EquipmentModelId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ManufacturerDocuments_Equipment_UploadedByEquipmentId",
                table: "ManufacturerDocuments",
                column: "UploadedByEquipmentId",
                principalTable: "Equipment",
                principalColumn: "EquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecommendations_EquipmentModels_EquipmentModelId",
                table: "MaintenanceRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecommendations_Equipment_EquipmentId",
                table: "MaintenanceRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ManufacturerDocuments_EquipmentModels_EquipmentModelId",
                table: "ManufacturerDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManufacturerDocuments_Equipment_UploadedByEquipmentId",
                table: "ManufacturerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ManufacturerDocuments_UploadedByEquipmentId",
                table: "ManufacturerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecommendations_EquipmentModelId",
                table: "MaintenanceRecommendations");

            migrationBuilder.DropColumn(
                name: "UploadedByEquipmentId",
                table: "ManufacturerDocuments");

            migrationBuilder.DropColumn(
                name: "EquipmentModelId",
                table: "MaintenanceRecommendations");

            migrationBuilder.RenameColumn(
                name: "EquipmentModelId",
                table: "ManufacturerDocuments",
                newName: "EquipmentId");

            migrationBuilder.RenameIndex(
                name: "IX_ManufacturerDocuments_EquipmentModelId",
                table: "ManufacturerDocuments",
                newName: "IX_ManufacturerDocuments_EquipmentId");

            migrationBuilder.AlterColumn<int>(
                name: "EquipmentId",
                table: "MaintenanceRecommendations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecommendations_Equipment_EquipmentId",
                table: "MaintenanceRecommendations",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "EquipmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ManufacturerDocuments_Equipment_EquipmentId",
                table: "ManufacturerDocuments",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "EquipmentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
