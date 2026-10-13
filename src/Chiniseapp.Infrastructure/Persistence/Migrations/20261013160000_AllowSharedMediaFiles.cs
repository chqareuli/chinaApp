using Microsoft.EntityFrameworkCore.Migrations;
namespace Chiniseapp.Infrastructure.Persistence.Migrations;
public partial class AllowSharedMediaFiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP INDEX IF EXISTS ix_media_assets_safe_storage_name; CREATE INDEX ix_media_assets_safe_storage_name ON media_assets (safe_storage_name);");
    // Multiple live placements may reference the same file; keep them when reverting code.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
