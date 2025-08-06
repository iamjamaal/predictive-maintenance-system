using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FEENALOoFINALE.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSeedDataWithHigherIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Equipment",
                columns: new[] { "EquipmentId", "AverageWeeklyUsageHours", "BuildingId", "EquipmentModelId", "EquipmentTypeId", "ExpectedLifespanMonths", "InstallationDate", "Notes", "RoomId", "Status" },
                values: new object[,]
                {
                    { 100, 25.5, 1, 1, 1, 60, new DateTime(2024, 1, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 1, 0 },
                    { 101, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 1, 0 },
                    { 102, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 1, 0 },
                    { 103, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 1, 0 },
                    { 104, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 1, 0 },
                    { 105, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 1, 0 },
                    { 106, 25.5, 1, 1, 1, 60, new DateTime(2024, 2, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 2, 0 },
                    { 107, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 2, 0 },
                    { 108, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 2, 0 },
                    { 109, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 2, 0 },
                    { 110, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 2, 0 },
                    { 111, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 2, 0 },
                    { 112, 25.5, 1, 1, 1, 60, new DateTime(2024, 2, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 3, 0 },
                    { 113, 25.5, 1, 4, 1, 60, new DateTime(2024, 2, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #2", 3, 0 },
                    { 114, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 3, 0 },
                    { 115, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 3, 0 },
                    { 116, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 3, 0 },
                    { 117, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 3, 0 },
                    { 118, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 3, 0 },
                    { 119, 25.5, 1, 1, 1, 60, new DateTime(2024, 2, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 4, 0 },
                    { 120, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 4, 0 },
                    { 121, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 4, 0 },
                    { 122, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 4, 0 },
                    { 123, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 4, 0 },
                    { 124, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 4, 0 },
                    { 125, 25.5, 1, 1, 1, 60, new DateTime(2024, 3, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 5, 0 },
                    { 126, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 5, 0 },
                    { 127, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 5, 0 },
                    { 128, 40.0, 1, 2, 2, 84, new DateTime(2024, 2, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 5, 0 },
                    { 129, 40.0, 1, 3, 2, 84, new DateTime(2024, 2, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 5, 0 },
                    { 130, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 5, 0 },
                    { 131, 25.5, 1, 1, 1, 60, new DateTime(2024, 3, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 6, 0 },
                    { 132, 25.5, 1, 4, 1, 60, new DateTime(2024, 3, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #2", 6, 0 },
                    { 133, 40.0, 1, 2, 2, 84, new DateTime(2024, 3, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 6, 0 },
                    { 134, 40.0, 1, 3, 2, 84, new DateTime(2024, 3, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 6, 0 },
                    { 135, 40.0, 1, 2, 2, 84, new DateTime(2024, 3, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 6, 0 },
                    { 136, 40.0, 1, 3, 2, 84, new DateTime(2024, 3, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #4", 6, 0 },
                    { 137, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 6, 0 },
                    { 138, 25.5, 1, 1, 1, 60, new DateTime(2024, 3, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 7, 0 },
                    { 139, 40.0, 1, 2, 2, 84, new DateTime(2024, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 7, 0 },
                    { 140, 40.0, 1, 3, 2, 84, new DateTime(2024, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 7, 0 },
                    { 141, 40.0, 1, 2, 2, 84, new DateTime(2024, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 7, 0 },
                    { 142, 20.0, 1, 5, 3, 120, new DateTime(2024, 3, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 7, 0 },
                    { 143, 25.5, 2, 1, 1, 60, new DateTime(2024, 4, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 8, 0 },
                    { 144, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 8, 0 },
                    { 145, 40.0, 2, 3, 2, 84, new DateTime(2024, 3, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 8, 0 },
                    { 146, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 8, 0 },
                    { 147, 20.0, 2, 5, 3, 120, new DateTime(2024, 3, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 8, 0 },
                    { 148, 25.5, 2, 1, 1, 60, new DateTime(2024, 4, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 9, 0 },
                    { 149, 25.5, 2, 4, 1, 60, new DateTime(2024, 4, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #2", 9, 0 },
                    { 150, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 9, 0 },
                    { 151, 40.0, 2, 3, 2, 84, new DateTime(2024, 3, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 9, 0 },
                    { 152, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 9, 0 },
                    { 153, 20.0, 2, 5, 3, 120, new DateTime(2024, 3, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 9, 0 },
                    { 154, 25.5, 2, 1, 1, 60, new DateTime(2024, 4, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 10, 0 },
                    { 155, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 10, 0 },
                    { 156, 40.0, 2, 3, 2, 84, new DateTime(2024, 3, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 10, 0 },
                    { 157, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 10, 0 },
                    { 158, 20.0, 2, 5, 3, 120, new DateTime(2024, 3, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 10, 0 },
                    { 159, 25.5, 2, 1, 1, 60, new DateTime(2024, 5, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 11, 0 },
                    { 160, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 11, 0 },
                    { 161, 40.0, 2, 3, 2, 84, new DateTime(2024, 3, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 11, 0 },
                    { 162, 40.0, 2, 2, 2, 84, new DateTime(2024, 3, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 11, 0 },
                    { 163, 20.0, 2, 5, 3, 120, new DateTime(2024, 4, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 11, 0 },
                    { 164, 25.5, 2, 1, 1, 60, new DateTime(2024, 5, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #1", 12, 0 },
                    { 165, 25.5, 2, 4, 1, 60, new DateTime(2024, 5, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classroom projector #2", 12, 0 },
                    { 166, 40.0, 2, 2, 2, 84, new DateTime(2024, 4, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #1", 12, 0 },
                    { 167, 40.0, 2, 3, 2, 84, new DateTime(2024, 4, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #2", 12, 0 },
                    { 168, 40.0, 2, 2, 2, 84, new DateTime(2024, 4, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "HVAC unit #3", 12, 0 },
                    { 169, 20.0, 2, 5, 3, 120, new DateTime(2024, 4, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lecture podium", 12, 0 },
                    { 170, 0.0, 1, 4, 1, 60, new DateTime(2023, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Old projector - needs repair", 7, 1 },
                    { 171, 0.0, 2, 3, 2, 84, new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Retired air conditioner - end of life", 12, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "EquipmentId",
                keyValue: 171);
        }
    }
}
