using Microsoft.EntityFrameworkCore.Migrations;
namespace Chiniseapp.Infrastructure.Persistence.Migrations;
public partial class PopulateMediaDisplayNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "UPDATE media_assets SET display_name = regexp_replace(original_filename, '\\.[^.]+$', '') WHERE display_name IS NULL;");
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
