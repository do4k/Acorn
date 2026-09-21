using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acorn.Database.PostgreSql.Migrations;

/// <inheritdoc />
public partial class _20260920214655_AddCharacterJailAndFreeze : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Frozen",
            table: "Characters",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Jailed",
            table: "Characters",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Frozen",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "Jailed",
            table: "Characters");
    }
}
