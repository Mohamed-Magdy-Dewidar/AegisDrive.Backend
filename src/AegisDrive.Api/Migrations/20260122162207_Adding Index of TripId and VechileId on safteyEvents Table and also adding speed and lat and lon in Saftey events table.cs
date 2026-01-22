using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AegisDrive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddingIndexofTripIdandVechileIdonsafteyEventsTableandalsoaddingspeedandlatandloninSafteyeventstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SafetyEvents_VehicleId",
                table: "SafetyEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Trips",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SafetyEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SafetyEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Speed",
                table: "SafetyEvents",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "TripId",
                table: "SafetyEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_TripId",
                table: "SafetyEvents",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_VehicleId_TripId",
                table: "SafetyEvents",
                columns: new[] { "VehicleId", "TripId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SafetyEvents_Trips_TripId",
                table: "SafetyEvents",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SafetyEvents_Trips_TripId",
                table: "SafetyEvents");

            migrationBuilder.DropIndex(
                name: "IX_SafetyEvents_TripId",
                table: "SafetyEvents");

            migrationBuilder.DropIndex(
                name: "IX_SafetyEvents_VehicleId_TripId",
                table: "SafetyEvents");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SafetyEvents");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SafetyEvents");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "SafetyEvents");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "SafetyEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_VehicleId",
                table: "SafetyEvents",
                column: "VehicleId");
        }
    }
}
