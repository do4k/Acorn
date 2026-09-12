using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acorn.Database.Migrations;

/// <inheritdoc />
public partial class _20260912182944_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Username = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Password = table.Column<string>(type: "TEXT", nullable: false),
                Salt = table.Column<string>(type: "TEXT", nullable: false),
                FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", x => x.Username);
            });

        migrationBuilder.CreateTable(
            name: "BoardPosts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                AuthorAdmin = table.Column<int>(type: "INTEGER", nullable: false),
                Subject = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Body = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BoardPosts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Guilds",
            columns: table => new
            {
                Tag = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                Ranks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                Bank = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Guilds", x => x.Tag);
            });

        migrationBuilder.CreateTable(
            name: "Characters",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Accounts_Username = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Home = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Fiance = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                Partner = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                Admin = table.Column<int>(type: "INTEGER", nullable: false),
                Class = table.Column<int>(type: "INTEGER", nullable: false),
                Gender = table.Column<int>(type: "INTEGER", nullable: false),
                Race = table.Column<int>(type: "INTEGER", nullable: false),
                HairStyle = table.Column<int>(type: "INTEGER", nullable: false),
                HairColor = table.Column<int>(type: "INTEGER", nullable: false),
                Map = table.Column<int>(type: "INTEGER", nullable: false),
                X = table.Column<int>(type: "INTEGER", nullable: false),
                Y = table.Column<int>(type: "INTEGER", nullable: false),
                Direction = table.Column<int>(type: "INTEGER", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                Exp = table.Column<int>(type: "INTEGER", nullable: false),
                MaxHp = table.Column<int>(type: "INTEGER", nullable: false),
                Hp = table.Column<int>(type: "INTEGER", nullable: false),
                MaxTp = table.Column<int>(type: "INTEGER", nullable: false),
                Tp = table.Column<int>(type: "INTEGER", nullable: false),
                MaxSp = table.Column<int>(type: "INTEGER", nullable: false),
                Sp = table.Column<int>(type: "INTEGER", nullable: false),
                Str = table.Column<int>(type: "INTEGER", nullable: false),
                Int = table.Column<int>(name: "\"Int\"", type: "INTEGER", nullable: false),
                Wis = table.Column<int>(type: "INTEGER", nullable: false),
                Agi = table.Column<int>(type: "INTEGER", nullable: false),
                Con = table.Column<int>(type: "INTEGER", nullable: false),
                Cha = table.Column<int>(type: "INTEGER", nullable: false),
                MinDamage = table.Column<int>(type: "INTEGER", nullable: false),
                MaxDamage = table.Column<int>(type: "INTEGER", nullable: false),
                MaxWeight = table.Column<int>(type: "INTEGER", nullable: false),
                Accuracy = table.Column<int>(type: "INTEGER", nullable: false),
                Evade = table.Column<int>(type: "INTEGER", nullable: false),
                Armor = table.Column<int>(type: "INTEGER", nullable: false),
                StatPoints = table.Column<int>(type: "INTEGER", nullable: false),
                SkillPoints = table.Column<int>(type: "INTEGER", nullable: false),
                Karma = table.Column<int>(type: "INTEGER", nullable: false),
                SitState = table.Column<int>(type: "INTEGER", nullable: false),
                Hidden = table.Column<bool>(type: "INTEGER", nullable: false),
                NoInteract = table.Column<bool>(type: "INTEGER", nullable: false),
                BankMax = table.Column<int>(type: "INTEGER", nullable: false),
                GoldBank = table.Column<int>(type: "INTEGER", nullable: false),
                Usage = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Characters", x => x.Id);
                table.UniqueConstraint("AK_Characters_Name", x => x.Name);
                table.ForeignKey(
                    name: "FK_Characters_Accounts_Accounts_Username",
                    column: x => x.Accounts_Username,
                    principalTable: "Accounts",
                    principalColumn: "Username",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GuildMembers",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                GuildTag = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                RankIndex = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildMembers", x => x.Id);
                table.ForeignKey(
                    name: "FK_GuildMembers_Guilds_GuildTag",
                    column: x => x.GuildTag,
                    principalTable: "Guilds",
                    principalColumn: "Tag",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CharacterItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<int>(type: "INTEGER", nullable: false),
                Slot = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_CharacterItems_Characters_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "Characters",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CharacterPaperdolls",
            columns: table => new
            {
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Hat = table.Column<int>(type: "INTEGER", nullable: false),
                Necklace = table.Column<int>(type: "INTEGER", nullable: false),
                Armor = table.Column<int>(type: "INTEGER", nullable: false),
                Belt = table.Column<int>(type: "INTEGER", nullable: false),
                Boots = table.Column<int>(type: "INTEGER", nullable: false),
                Gloves = table.Column<int>(type: "INTEGER", nullable: false),
                Weapon = table.Column<int>(type: "INTEGER", nullable: false),
                Shield = table.Column<int>(type: "INTEGER", nullable: false),
                Accessory = table.Column<int>(type: "INTEGER", nullable: false),
                Ring1 = table.Column<int>(type: "INTEGER", nullable: false),
                Ring2 = table.Column<int>(type: "INTEGER", nullable: false),
                Bracer1 = table.Column<int>(type: "INTEGER", nullable: false),
                Bracer2 = table.Column<int>(type: "INTEGER", nullable: false),
                Armlet1 = table.Column<int>(type: "INTEGER", nullable: false),
                Armlet2 = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterPaperdolls", x => x.CharacterName);
                table.ForeignKey(
                    name: "FK_CharacterPaperdolls_Characters_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "Characters",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CharacterSpells",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                SpellId = table.Column<int>(type: "INTEGER", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterSpells", x => x.Id);
                table.ForeignKey(
                    name: "FK_CharacterSpells_Characters_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "Characters",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "QuestProgress",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                QuestId = table.Column<int>(type: "INTEGER", nullable: false),
                State = table.Column<int>(type: "INTEGER", nullable: false),
                NpcKillsJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                PlayerKills = table.Column<int>(type: "INTEGER", nullable: false),
                DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Completions = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuestProgress", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuestProgress_Characters_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "Characters",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BoardPosts_BoardId_Id",
            table: "BoardPosts",
            columns: new[] { "BoardId", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_BoardPosts_CharacterName",
            table: "BoardPosts",
            column: "CharacterName");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterItems_CharacterName_Slot",
            table: "CharacterItems",
            columns: new[] { "CharacterName", "Slot" });

        migrationBuilder.CreateIndex(
            name: "IX_Characters_Accounts_Username",
            table: "Characters",
            column: "Accounts_Username");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterSpells_CharacterName",
            table: "CharacterSpells",
            column: "CharacterName");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterSpells_CharacterName_SpellId",
            table: "CharacterSpells",
            columns: new[] { "CharacterName", "SpellId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GuildMembers_CharacterName",
            table: "GuildMembers",
            column: "CharacterName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GuildMembers_GuildTag",
            table: "GuildMembers",
            column: "GuildTag");

        migrationBuilder.CreateIndex(
            name: "IX_QuestProgress_CharacterName",
            table: "QuestProgress",
            column: "CharacterName");

        migrationBuilder.CreateIndex(
            name: "IX_QuestProgress_CharacterName_QuestId",
            table: "QuestProgress",
            columns: new[] { "CharacterName", "QuestId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BoardPosts");

        migrationBuilder.DropTable(
            name: "CharacterItems");

        migrationBuilder.DropTable(
            name: "CharacterPaperdolls");

        migrationBuilder.DropTable(
            name: "CharacterSpells");

        migrationBuilder.DropTable(
            name: "GuildMembers");

        migrationBuilder.DropTable(
            name: "QuestProgress");

        migrationBuilder.DropTable(
            name: "Guilds");

        migrationBuilder.DropTable(
            name: "Characters");

        migrationBuilder.DropTable(
            name: "Accounts");
    }
}
