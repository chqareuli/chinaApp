using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrySupportMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entry_support_materials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    added_by_editor_id = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    comment_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_support_materials", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_support_materials_editors_added_by_editor_id",
                        column: x => x.added_by_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entry_support_materials_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entry_support_materials_added_by_editor_id",
                table: "entry_support_materials",
                column: "added_by_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_entry_support_materials_entry_id_created_at_utc",
                table: "entry_support_materials",
                columns: new[] { "entry_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_support_materials");
        }
    }
}
