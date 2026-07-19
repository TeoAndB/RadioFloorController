using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadioFloorController.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorTimeoutTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "FloorGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastHolderUserId",
                table: "FloorGrants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastReleaseReason",
                table: "FloorGrants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReleasedAt",
                table: "FloorGrants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "FloorGrants");

            migrationBuilder.DropColumn(
                name: "LastHolderUserId",
                table: "FloorGrants");

            migrationBuilder.DropColumn(
                name: "LastReleaseReason",
                table: "FloorGrants");

            migrationBuilder.DropColumn(
                name: "LastReleasedAt",
                table: "FloorGrants");
        }
    }
}
