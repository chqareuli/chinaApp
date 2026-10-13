using Microsoft.EntityFrameworkCore.Migrations;
namespace Chiniseapp.Infrastructure.Persistence.Migrations;
public partial class AddMediaDisplayName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "display_name", table: "media_assets", type: "text", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "display_name", table: "media_assets");
}
