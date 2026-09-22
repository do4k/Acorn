using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acorn.Database.Migrations;

/// <inheritdoc />
public partial class _20260921212912_CleanupPhantomQuestProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Remove phantom quest progress created by the GetOrCreateProgress-in-dialog-
        // filter bug: talking to any quest NPC assigned State 0 rows for every quest
        // in the repository. Started quests, dailies (DoneAt set) and quests with
        // tracked kills are preserved.
        migrationBuilder.Sql("""
            DELETE FROM "QuestProgress"
            WHERE "State" = 0
              AND "DoneAt" IS NULL
              AND "Completions" = 0
              AND "PlayerKills" = 0
              AND "NpcKillsJson" = '{}'
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data cleanup; nothing to restore.
    }
}
