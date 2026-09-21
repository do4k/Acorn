using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acorn.Database.Migrations;

/// <inheritdoc />
public partial class _20260920215251_AddBanTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Bans",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Bans", x => x.Key);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Bans");
    }
}
