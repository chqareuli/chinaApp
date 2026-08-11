using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChiniseDbContext))]
[Migration("20260811155000_AlignRolesAndScoreTypesWithScoreDocument")]
public partial class AlignRolesAndScoreTypesWithScoreDocument : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TYPE editor_role RENAME VALUE 'zh_editor' TO 'editor';");
        migrationBuilder.Sql("ALTER TYPE editor_role ADD VALUE IF NOT EXISTS 'deputy_chief_editor' AFTER 'chief_editor';");

        // A Georgian editor could previously have both the legacy ka_editor score and an
        // additional score for the same entry. Keep one Additional score before merging.
        migrationBuilder.Sql(
            """
            DELETE FROM score_entries AS legacy
            USING score_entries AS existing
            WHERE legacy.score_type = 'ka_editor'
              AND existing.score_type = 'additional'
              AND legacy.entry_id = existing.entry_id
              AND legacy.editor_id = existing.editor_id;

            UPDATE score_entries
            SET score_type = 'additional'
            WHERE score_type = 'ka_editor';

            ALTER TYPE score_type RENAME TO score_type_old;
            CREATE TYPE score_type AS ENUM ('main', 'additional');
            ALTER TABLE score_entries
                ALTER COLUMN score_type TYPE score_type
                USING score_type::text::score_type;
            DROP TYPE score_type_old;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The legacy Georgian-score classification cannot be reconstructed after it has
        // been merged into Additional, so rollback preserves those rows as Additional.
        migrationBuilder.Sql(
            """
            ALTER TYPE score_type RENAME TO score_type_new;
            CREATE TYPE score_type AS ENUM ('main', 'additional', 'ka_editor');
            ALTER TABLE score_entries
                ALTER COLUMN score_type TYPE score_type
                USING score_type::text::score_type;
            DROP TYPE score_type_new;

            UPDATE editors
            SET role = 'chief_editor'
            WHERE role = 'deputy_chief_editor';

            ALTER TYPE editor_role RENAME TO editor_role_new;
            CREATE TYPE editor_role AS ENUM (
                'super_admin', 'chief_editor', 'zh_editor', 'ka_editor', 'assistant_editor');
            ALTER TABLE editors
                ALTER COLUMN role TYPE editor_role
                USING CASE
                    WHEN role::text = 'editor' THEN 'zh_editor'::editor_role
                    ELSE role::text::editor_role
                END;
            DROP TYPE editor_role_new;
            """);
    }
}
