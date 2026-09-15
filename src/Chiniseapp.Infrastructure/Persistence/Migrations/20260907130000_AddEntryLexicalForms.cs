using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChiniseDbContext))]
[Migration("20260907130000_AddEntryLexicalForms")]
public class AddEntryLexicalForms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "lexical_forms", table: "entries", type: "jsonb", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "lexical_forms", table: "entries");
}
