using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryComplexForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entry_complex_forms",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_complex_forms", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_complex_forms_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entry_complex_forms_entry_id_normalized_value",
                table: "entry_complex_forms",
                columns: new[] { "entry_id", "normalized_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entry_complex_forms_entry_id_order_index",
                table: "entry_complex_forms",
                columns: new[] { "entry_id", "order_index" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_complex_forms");
        }
    }
}
