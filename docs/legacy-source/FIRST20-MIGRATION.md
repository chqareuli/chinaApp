# First 20 article migration — 2026-11-04

Source: read-only snapshot of `old_dictionary` on 116.203.24.175, first 20
`articles` ordered by ID (1–20). Included latest comments, historical workflow,
author/time metadata, full article history and the 28 secondary example rows.
Destination: `chiniseapp` on the user-confirmed test server 178.104.138.129.

All 20 records were inserted as repair-flagged `new_entry` drafts, destination
IDs **35–54**. The migration operator is editor ID 1; old author identities and
dates are preserved in the immutable source snapshot rather than attributed
to unrelated new editor accounts. No source records were changed.

Validation:

- 27 focused parser and mapper tests passed.
- Backed up test database and restored separate `chiniseapp_legacy20_20261104`.
- Rehearsal imported 20 drafts; repeat imported zero and recognized all 20.
- Backed up again immediately before applying to the designated test database.
- Apply imported 20 drafts; repeat imported zero and recognized all 20.
- Compared all 34 pre-existing entry rows and all 547 pre-existing segment rows
  against the pre-import snapshot: unchanged.
- API `/health/db` returned `{"status":"ok","database":"reachable"}`.
- Persisted source snapshots and entry trees were read back for the review report.

Review exceptions: 3 examples with no Georgian text (their cross-references
remain attached); 6 secondary-example discrepancies preserved as internal
comments; 9 cross-reference targets not present in the destination; one entry
with no definition terminator. All 20 retain the staging editorial-review flag.
These are draft review items, not claims that semantic accuracy is fully accepted.
The full editor UI has not been visually checked in this run.

Backups (root-readable, on test server):

```
/opt/chiniseapp/backups/legacy-first20-20261104/before-rehearsal.dump
/opt/chiniseapp/backups/legacy-first20-20261104/before-apply.dump
```

Tool, source snapshot and apply/repeat reports on the server:
`/opt/chiniseapp/tools/legacy-first20-20261104/`.
Local reports under ignored `TestResults/`: `legacy-first20-source.json`,
`legacy-first20-applied.json`, `legacy-first20-persisted.json`, and
`legacy-first20-review.html`. These contain editorial data and are not committed.

Review in the test editor using IDs 35–54 or their Chinese headwords. Inspect
the New Entry/repair filters when looking for these drafts. Further corpus batches
require additional coverage for homographs, neutralized links and other edge cases.
