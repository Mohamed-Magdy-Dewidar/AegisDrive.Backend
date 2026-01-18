using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AegisDrive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTripEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartLat = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    StartLng = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    EndLat = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    EndLng = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DestinationText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DestinationLat = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    DestinationLng = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    EstimatedDistanceMeters = table.Column<double>(type: "float", nullable: false),
                    EstimatedDurationSeconds = table.Column<double>(type: "float", nullable: false),
                    RouteGeometryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TripSafetyScore = table.Column<double>(type: "float", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId",
                table: "Trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId",
                table: "Trips",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trips");
        }
    }
}
