using Microsoft.EntityFrameworkCore.Migrations;
namespace Chiniseapp.Infrastructure.Persistence.Migrations;
public partial class AddCommentEntryLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name:"entry_links",table:"comments",type:"jsonb",nullable:true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name:"entry_links",table:"comments");
}
