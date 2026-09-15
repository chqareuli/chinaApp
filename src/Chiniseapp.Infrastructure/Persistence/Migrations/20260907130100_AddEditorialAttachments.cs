using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some local installations already have this additive media association.
            migrationBuilder.Sql("ALTER TABLE media_assets ADD COLUMN IF NOT EXISTS entry_id integer;");

            migrationBuilder.CreateTable(
                name: "support_materials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    author_editor_id = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_materials", x => x.id);
                    table.ForeignKey(
                        name: "fk_support_materials_editors_author_editor_id",
                        column: x => x.author_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_support_materials_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_media_assets_entry_id ON media_assets (entry_id);");

            migrationBuilder.CreateIndex(
                name: "ix_support_materials_author_editor_id",
                table: "support_materials",
                column: "author_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_materials_entry_id",
                table: "support_materials",
                column: "entry_id");

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_media_assets_entries_entry_id' AND conrelid = 'media_assets'::regclass) THEN
                        ALTER TABLE media_assets ADD CONSTRAINT fk_media_assets_entries_entry_id FOREIGN KEY (entry_id) REFERENCES entries(id) ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_media_assets_entries_entry_id",
                table: "media_assets");

            migrationBuilder.DropTable(
                name: "support_materials");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_entry_id",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "entry_id",
                table: "media_assets");
        }
    }
}
