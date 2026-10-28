using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChiniseDbContext))]
[Migration("20261028160000_AddUniqueEntryTitleIndex")]
public class AddUniqueEntryTitleIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX ix_entries_title_key;
        CREATE UNIQUE INDEX uq_entries_lemma_title ON entries (entry_title_key(lemma));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX uq_entries_lemma_title;
        CREATE INDEX ix_entries_title_key ON entries (entry_title_key(lemma));
        """);
}
