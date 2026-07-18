using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadioFloorController.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FloorGrants",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "text", nullable: false),
                    HolderUserId = table.Column<string>(type: "text", nullable: true),
                    ObtainedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorGrants", x => x.GroupId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloorGrants");
        }
    }
}
