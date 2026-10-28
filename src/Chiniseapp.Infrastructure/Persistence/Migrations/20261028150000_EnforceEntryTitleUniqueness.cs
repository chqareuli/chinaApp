using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChiniseDbContext))]
[Migration("20261028150000_EnforceEntryTitleUniqueness")]
public class EnforceEntryTitleUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION entry_title_key(text) RETURNS text
        LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $$
          SELECT lower(normalize(btrim(replace($1, '，', ',')), NFC))
        $$;

        -- One unique reservation per title, including titles shared by legacy rows.
        -- Existing editorial content is retained until its owners resolve that pair.
        CREATE TABLE entry_title_keys (title_key text CONSTRAINT uq_entries_title PRIMARY KEY);
        INSERT INTO entry_title_keys SELECT DISTINCT entry_title_key(lemma) FROM entries;
        CREATE INDEX ix_entries_title_key ON entries (entry_title_key(lemma));

        CREATE FUNCTION reserve_entry_title() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'INSERT' THEN
            INSERT INTO entry_title_keys VALUES (entry_title_key(NEW.lemma));
          ELSIF entry_title_key(NEW.lemma) IS DISTINCT FROM entry_title_key(OLD.lemma) THEN
            PERFORM pg_advisory_xact_lock(hashtextextended('entry-title:' || entry_title_key(OLD.lemma), 0));
            INSERT INTO entry_title_keys VALUES (entry_title_key(NEW.lemma));
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER reserve_entry_title BEFORE INSERT OR UPDATE OF lemma ON entries
          FOR EACH ROW EXECUTE FUNCTION reserve_entry_title();

        CREATE FUNCTION release_entry_title() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM pg_advisory_xact_lock(hashtextextended('entry-title:' || entry_title_key(OLD.lemma), 0));
          DELETE FROM entry_title_keys WHERE title_key = entry_title_key(OLD.lemma)
            AND NOT EXISTS (SELECT 1 FROM entries WHERE entry_title_key(lemma) = entry_title_key(OLD.lemma));
          RETURN NULL;
        END $$;
        CREATE TRIGGER release_entry_title AFTER DELETE OR UPDATE OF lemma ON entries
          FOR EACH ROW EXECUTE FUNCTION release_entry_title();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER release_entry_title ON entries;
        DROP TRIGGER reserve_entry_title ON entries;
        DROP FUNCTION release_entry_title();
        DROP FUNCTION reserve_entry_title();
        DROP INDEX ix_entries_title_key;
        DROP TABLE entry_title_keys;
        DROP FUNCTION entry_title_key(text);
        """);
}
