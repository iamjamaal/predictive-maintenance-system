using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FEENALOoFINALE.Migrations
{
    /// <inheritdoc />
    public partial class MoreTimeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Semesters",
                columns: table => new
                {
                    SemesterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SemesterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumberOfWeeks = table.Column<int>(type: "int", nullable: false),
                    TimetableFilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessingMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquipmentUsageDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalEquipmentHours = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Semesters", x => x.SemesterId);
                    table.ForeignKey(
                        name: "FK_Semesters_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SemesterEquipmentUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SemesterId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    WeeklyUsageHours = table.Column<double>(type: "float(8)", precision: 8, scale: 2, nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsagePatternJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemesterEquipmentUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemesterEquipmentUsages_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "EquipmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SemesterEquipmentUsages_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "SemesterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemesterEquipmentUsage_Semester_Equipment",
                table: "SemesterEquipmentUsages",
                columns: new[] { "SemesterId", "EquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SemesterEquipmentUsages_EquipmentId",
                table: "SemesterEquipmentUsages",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_UploadedByUserId",
                table: "Semesters",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemesterEquipmentUsages");

            migrationBuilder.DropTable(
                name: "Semesters");
        }
    }
}
