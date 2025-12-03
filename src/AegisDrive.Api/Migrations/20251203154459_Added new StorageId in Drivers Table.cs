using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AegisDrive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddednewStorageIdinDriversTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "storageId",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "storageId",
                table: "Drivers");
        }
    }
}
