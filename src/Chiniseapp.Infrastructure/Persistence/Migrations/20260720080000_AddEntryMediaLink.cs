using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChiniseDbContext))]
[Migration("20260720080000_AddEntryMediaLink")]
public partial class AddEntryMediaLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "entry_id",
            table: "media_assets",
            type: "integer",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "placement",
            table: "media_assets",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_entry_id",
            table: "media_assets",
            column: "entry_id");

        migrationBuilder.CreateIndex(
            name: "ix_media_assets_safe_storage_name",
            table: "media_assets",
            column: "safe_storage_name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_media_assets_entries_entry_id",
            table: "media_assets",
            column: "entry_id",
            principalTable: "entries",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_media_assets_entries_entry_id",
            table: "media_assets");

        migrationBuilder.DropIndex(
            name: "ix_media_assets_entry_id",
            table: "media_assets");

        migrationBuilder.DropIndex(
            name: "ix_media_assets_safe_storage_name",
            table: "media_assets");

        migrationBuilder.DropColumn(
            name: "entry_id",
            table: "media_assets");

        migrationBuilder.AlterColumn<string>(
            name: "placement",
            table: "media_assets",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);
    }
}
