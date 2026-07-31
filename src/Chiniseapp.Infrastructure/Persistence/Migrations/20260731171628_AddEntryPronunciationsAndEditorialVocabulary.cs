using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryPronunciationsAndEditorialVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entry_pronunciations",
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
                    table.PrimaryKey("pk_entry_pronunciations", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_pronunciations_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entry_pronunciations_entry_id_normalized_value",
                table: "entry_pronunciations",
                columns: new[] { "entry_id", "normalized_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entry_pronunciations_entry_id_order_index",
                table: "entry_pronunciations",
                columns: new[] { "entry_id", "order_index" });

            migrationBuilder.Sql(
                """
                INSERT INTO entry_pronunciations
                    (entry_id, value, normalized_value, order_index, created_at_utc, updated_at_utc)
                SELECT id, btrim(pinyin), upper(btrim(pinyin)), 0, created_at_utc, updated_at_utc
                FROM entries
                WHERE pinyin IS NOT NULL AND btrim(pinyin) <> '';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO controlled_vocabularies
                    (category, code, display_zh, display_ka, sort_order, is_active)
                VALUES
                    ('pos'::vocabulary_category, '名', '名', NULL, 1, TRUE),
                    ('pos'::vocabulary_category, '动', '动', NULL, 2, TRUE),
                    ('pos'::vocabulary_category, '形', '形', NULL, 3, TRUE),
                    ('pos'::vocabulary_category, '副', '副', NULL, 4, TRUE),
                    ('pos'::vocabulary_category, '代', '代', NULL, 5, TRUE),
                    ('pos'::vocabulary_category, '量', '量', NULL, 6, TRUE),
                    ('pos'::vocabulary_category, '数', '数', NULL, 7, TRUE),
                    ('pos'::vocabulary_category, '介', '介', NULL, 8, TRUE),
                    ('pos'::vocabulary_category, '连', '连', NULL, 9, TRUE),
                    ('pos'::vocabulary_category, '助', '助', NULL, 10, TRUE),
                    ('pos'::vocabulary_category, '叹', '叹', NULL, 11, TRUE),
                    ('pos'::vocabulary_category, '象', '象', NULL, 12, TRUE),
                    ('pos'::vocabulary_category, '缀', '缀', NULL, 13, TRUE),
                    ('pos'::vocabulary_category, '素', '素', NULL, 14, TRUE),
                    ('pos'::vocabulary_category, '音', '音', NULL, 15, TRUE),
                    ('style'::vocabulary_category, '插', '〈插〉', NULL, 1, TRUE),
                    ('style'::vocabulary_category, '成', '〈成〉', NULL, 2, TRUE),
                    ('style'::vocabulary_category, '粗', '〈粗〉', NULL, 3, TRUE),
                    ('style'::vocabulary_category, '方', '〈方〉', NULL, 4, TRUE),
                    ('style'::vocabulary_category, '讽', '〈讽〉', NULL, 5, TRUE),
                    ('style'::vocabulary_category, '多讽', '〈多讽〉', NULL, 6, TRUE),
                    ('style'::vocabulary_category, '古', '〈古〉', NULL, 7, TRUE),
                    ('style'::vocabulary_category, '惯', '〈惯〉', NULL, 8, TRUE),
                    ('style'::vocabulary_category, '诙', '〈诙〉', NULL, 9, TRUE),
                    ('style'::vocabulary_category, '讳', '〈讳〉', NULL, 10, TRUE),
                    ('domain'::vocabulary_category, '材', '【材】', NULL, 1, TRUE),
                    ('domain'::vocabulary_category, '测', '【测】', NULL, 2, TRUE),
                    ('domain'::vocabulary_category, '船', '【船】', NULL, 3, TRUE),
                    ('domain'::vocabulary_category, '道', '【道】', NULL, 4, TRUE),
                    ('domain'::vocabulary_category, '地理', '【地理】', NULL, 5, TRUE),
                    ('domain'::vocabulary_category, '地质', '【地质】', NULL, 6, TRUE),
                    ('domain'::vocabulary_category, '电', '【电】', NULL, 7, TRUE),
                    ('domain'::vocabulary_category, '电子', '【电子】', NULL, 8, TRUE),
                    ('domain'::vocabulary_category, '动', '【动】', NULL, 9, TRUE),
                    ('domain'::vocabulary_category, '法', '【法】', NULL, 10, TRUE),
                    ('abbr'::vocabulary_category, 'ჩვეულ.', 'ჩვეულ.', NULL, 1, TRUE),
                    ('abbr'::vocabulary_category, 'ითქმ.', 'ითქმ.', NULL, 2, TRUE),
                    ('abbr'::vocabulary_category, 'ა.შ.', 'ა.შ.', NULL, 3, TRUE),
                    ('abbr'::vocabulary_category, 'გადატ.', 'გადატ.', NULL, 4, TRUE),
                    ('abbr'::vocabulary_category, 'ახ. წ.', 'ახ. წ.', NULL, 5, TRUE),
                    ('lang'::vocabulary_category, 'ლათ.', 'ლათ.', NULL, 1, TRUE),
                    ('lang'::vocabulary_category, 'ინგ.', 'ინგ.', NULL, 2, TRUE),
                    ('lang'::vocabulary_category, 'ფრანგ.', 'ფრანგ.', NULL, 3, TRUE),
                    ('lang'::vocabulary_category, 'სანსკრ.', 'სანსკრ.', NULL, 4, TRUE),
                    ('lang'::vocabulary_category, 'ესპ.', 'ესპ.', NULL, 5, TRUE)
                ON CONFLICT (category, code) DO UPDATE SET
                    display_zh = EXCLUDED.display_zh,
                    sort_order = EXCLUDED.sort_order,
                    is_active = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM controlled_vocabularies
                WHERE (category = 'pos'::vocabulary_category AND code IN
                    ('名','动','形','副','代','量','数','介','连','助','叹','象','缀','素','音'))
                   OR (category = 'style'::vocabulary_category AND code IN
                    ('插','成','粗','方','讽','多讽','古','惯','诙','讳'))
                   OR (category = 'domain'::vocabulary_category AND code IN
                    ('材','测','船','道','地理','地质','电','电子','动','法'))
                   OR (category = 'abbr'::vocabulary_category AND code IN
                    ('ჩვეულ.','ითქმ.','ა.შ.','გადატ.','ახ. წ.'))
                   OR (category = 'lang'::vocabulary_category AND code IN
                    ('ლათ.','ინგ.','ფრანგ.','სანსკრ.','ესპ.'));
                """);

            migrationBuilder.DropTable(
                name: "entry_pronunciations");
        }
    }
}
