# Screenshot corrections — 2026-10-26

Source: `C:/Users/NODO/Desktop/china/attachments`. EP20–30 repeat the previous review; the following seven images add the new scope.

- EP40: group and private conversations share latest-message ordering; inbox refreshes every 15 seconds without repeatedly jumping the reader to the bottom. Unread-count requests cannot restore an older badge after a read. Profile work counts link to matching entry/status/score filters and inherit stage colors; totals use one color. Home entry titles inherit stage colors. Stage confirmation is English/Chinese. Team Actions column is hidden for editors who cannot manage accounts.
- EP41: pinyin is larger and bold; read-only written forms wrap instead of truncating long text. Traditional forms are larger with their existing color. Definitions stay bold; Chinese and Georgian examples use normal weight. Style markers precede Domain markers while keeping the saved array indices for edits/removal.
- EP42: reference groups use named Expand/Collapse controls. Selecting G1–G5 expands that group. Pin becomes Unpin while active. A desktop reference pane reserves its own space; smaller screens keep it in document flow.
- EP45: compact read-only entry layout and smaller text controls. Saved entries have a Preview/Editor view toggle using the same saved content; it is disabled during editing. This does not deploy changes to the separate public chinese.ge application.
- EP46: media rows show every linked entry visible to the current editor. Rename preserves the supplied display name and rejects another image's existing name. Replacement updates the shared file and MIME/filename metadata across its uses while preserving media IDs, placements, names, and URLs. Both changes require content-edit permission for every affected entry. Reuse rereads metadata after its lock to avoid copying an obsolete name/MIME type.
- EP+47: replies display the parent comment's author and text in a distinct quote block, separate from the new response. Existing chronological ordering and entry links remain intact.
- EP48: search-result links carry the query into the entry. Matches in definitions and Chinese/Georgian examples are highlighted without changing saved text, references, or images. Adding a comment updates the entry timestamp, so the existing newest-first Home list promotes it; existing Additional scoring is preserved.

## Verification

- Backend integration tests cover shared-file rename/replacement, duplicate names, permissions, linked entries, stable URLs, and comment timestamps in isolated local PostgreSQL databases.
- Frontend regressions cover mixed conversation ordering, stale unread-count responses, highlighted text, and existing profile totals.
- Browser checks: Chinese example search highlighting; reserved reference-pane space; media management dialog and linked entries; profile work-filter links. Browser checks did not modify editorial content or send messages.
- Final automated results: 113 frontend tests across 24 files; 284 backend tests, zero skipped. Regression tests also protect inline text/cursor whitespace when highlighting is rendered.

Deferred by user: importing approximately 500 legacy photos and their original article mapping.
