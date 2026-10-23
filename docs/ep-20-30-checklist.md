# Screenshot corrections, 2026-10-23

Source: `C:/Users/NODO/Desktop/fixes for china/attachments` (11 images).
Each numbered line is a separate acceptance point. Repeated requirements refer to the same implementation.

- EP20: login subtitle color, size, and equal spacing above/below.
- EP21: table frame; inner padding; equal English/Chinese size; bold status labels/counts; uppercase bold headings; aligned totals.
- EP22: consistent bold editor names; stacked role codes for dual-role editors; team names blue and both names equal size; profile link and work summary.
- EP22a: team title color; horizontal margins; centered equal-size bilingual text; same name format as EP22.
- EP23: pencil opens role/status editing; name opens full profile; supervisor can correct registration fields; read-only profile/work statistics for other editors; larger edit control.
- EP24: XML export; JSON export; EP21 table format; compact work table; balanced score columns; equal bilingual headings; EP22 names.
- EP25: centered MEDIA LIBRARY title; bold author; image thumbnails; remove redundant All entries/entry links. Legacy image migration requires old files plus their name/article mapping.
- EP26: EP22 names in list/preview; retain unread dot/read behavior.
- EP27: EP22 names; centered recipient heading; newest messages easy to reach without scrolling the entire page.
- EP28: remove duplicate entry-open arrow; last-modified heading; include drafts when filters/search are applied on Home while default Home excludes them; bold pinyin; traditional form sizing/color; consistent homograph color; bold definitions; POS/style/domain compact symbols instead of long labels; italic abbreviations.
- EP30: A+/A− changes comment body as well as metadata.

External dependency: legacy image files and original name-to-article relationships are not present in this repository. Do not invent associations or import by filename guesses.

## Implementation and verification

The listed application changes are implemented. Legacy image migration remains pending the source files and mapping; no public-site image associations were guessed.

- Production frontend build passed; 109 frontend tests passed across 22 files.
- Backend suite passed: 283 tests, including legacy `xrType` cross-reference compatibility. Strict validation of new requests is retained.
- Local authenticated API checks returned HTTP 200 for XML, JSON, and helper-material exports. Legacy cross-references no longer interrupt export/tree loading.
- Home draft filtering has a regression test: default excludes drafts; an explicit stage/author filter includes them.
- Profile status-count rendering and totals have a regression test using the actual API response shape. Profile changes remain supervisor-only; colleagues can read profiles and work summaries.
- Browser checked statistics, team/profile fields, media thumbnails, and the bounded conversation area. Existing local editor registration records without separate first/last names require correction before saving a complete profile.
- Full authenticated export on the test server with its existing editorial data still needs a user-session check; local API success does not prove every server record is valid.
