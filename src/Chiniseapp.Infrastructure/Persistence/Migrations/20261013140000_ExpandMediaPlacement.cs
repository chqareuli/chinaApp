using Microsoft.EntityFrameworkCore.Migrations;

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

public partial class ExpandMediaPlacement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE media_assets ALTER COLUMN placement TYPE text;");

    // Keep existing inline positions intact when rolling back application code.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
