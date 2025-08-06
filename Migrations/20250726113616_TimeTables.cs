using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FEENALOoFINALE.Migrations
{
    /// <inheritdoc />
    public partial class TimeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<double>(
                name: "AverageWeeklyUsageHours",
                table: "Equipment",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentUsageHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsageHours = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentUsageHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentUsageHistories_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "EquipmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentUsageHistories_EquipmentId",
                table: "EquipmentUsageHistories",
                column: "EquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentUsageHistories");

            migrationBuilder.DropColumn(
                name: "AverageWeeklyUsageHours",
                table: "Equipment");

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
