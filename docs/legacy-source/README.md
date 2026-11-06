# Legacy migration tools

## Semantic staging parser (0.2)

`LegacySemanticParser` uses the local workbook's DB-START/END, LR-START-END and
ZH/KA rules. It supports simple pronunciation/traditional-form lists, roman POS
groups, sequential numbered senses, style/domain markers, Chinese/Georgian
example pairs and grouped cross-references with validated display offsets.
Source data is always retained. Homographs, neutralizers, unexpected prefixes,
unclassified Chinese definitions and invalid sense sequences block proposals.
This version is verified for the first-20 sample, not the entire corpus.

```powershell
dotnet run --project tools/LegacyParse -- input.json report.json --semantic
```

## Transactional test importer

`tools/LegacyImport` imports proposals as repair-flagged `new_entry` drafts.
It validates the operator and active vocabulary, uses one transaction and an
advisory lock, blocks destination headword conflicts, and stores source hashes,
original metadata/history and source-ID-to-entry-ID mapping in `LegacyImport`
audit rows. Repeated imports skip unchanged source IDs and never overwrite
existing entries or subsequent editorial work. Changed sources require review.
The old workflow/author IDs and timestamps stay in source metadata: destination
authorship is the explicitly selected migration operator, not a guessed identity.
Unresolved links retain display/target text. Supplementary `article_examples`
rows that do not exactly match parsed examples become internal review comments,
not guessed sense attachments. Full source rows remain in the audit snapshot.
No photo files or original user accounts are migrated by this pilot.

```powershell
# Set ConnectionStrings__DefaultConnection in the process environment.
dotnet run --project tools/LegacyImport -- --dry-run input.json source-system 1 report.json
# --apply commits; --dry-run exercises real writes and rolls back the transaction.
# PostgreSQL sequences can advance during a dry-run; prefer a database copy.
```

The first 20 were rehearsed on a restored test database and then applied to
178.104.138.129 on 2026-11-04. See `FIRST20-MIGRATION.md` for evidence.

## Original lossless analyzer (0.1)

`article.ejs` was read from `/root/dictionary-2021-05-10.tar.gz`, member
`old_dictionary/views/pages/article.ejs`, on the old server on 2026-11-04.
It is archived rendering code, not proof of the current .NET service's behavior.

Relevant source lines: translation 296–325, reference material 328–347,
comments 350–354, links 155–182, example-rendering heuristics 185–248.

The application parser keeps each source record unchanged, produces tokens with
original UTF-16 offsets, splits reference material into G1–G5, and identifies
comment spans. Asterisks are matched as complete runs (including fullwidth ＊);
repeated, backward, or oversized runs are retained and reported. Slashes stay
verbatim; display bullets belong in the renderer. Comment author markers do not
establish an editor identity.

In the original analyzer, only simple POS + definition records produce proposed application DTOs.
Homographs, roman groups, numbered senses, style/domain scope, Chinese runs,
examples and cross-reference resolution are explicitly reported for review.
No Chinese run is silently assumed to be an example. No original underscore,
punctuation or 丶 is deleted just because the old renderer hid it. Duplicate
headwords block both proposals. Missing POS/style is a structural exception.

Run from the repository root:

```powershell
dotnet run --project tools/LegacyParse -- docs/legacy-source/sample-input.json report.json
dotnet test tests/Chiniseapp.Tests --filter FullyQualifiedName~LegacyParserTests
```

Input is a JSON array with `sourceId`, `word`, `translation`, optional
`workingSet`, `comment`, `transcription`, and `traditional`. The report contains
all original rows, tokens, proposals, issue counts, and source fragments.
The checked-in sample is synthetic. The analyzer itself has no database connection
or writes. Proposed DTOs have no destination row version and must not be
sent directly to the save API.

Before expanding semantic conversion further, compare real corpus samples, the local
`Legacy_Parser_Source.xlsx`, the migration specification, and the deployed
services. Pronunciations/traditional forms, workflow/authorship, linked examples,
media, and other tables still require explicit mapping. Validate representative
samples and exception classes in staging before production cutover.
