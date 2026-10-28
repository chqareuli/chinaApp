# Unique article titles

Titles are reserved across every entry status, including drafts and archived entries. Matching ignores outer spaces, case, Unicode composed/decomposed differences, and the Chinese/ASCII comma variant already recognized by title search.

- Create and rename perform a server-side check and return HTTP 409 with `code: duplicate_title` and a Georgian explanation.
- The editor distinguishes this error from a stale-version conflict and preserves the unsaved form for correction.
- Migration `20261028150000_EnforceEntryTitleUniqueness` installs a canonical key function, a uniquely constrained title-reservation table, an expression index, and transactional triggers. Concurrent/direct database writes are protected by the unique constraint; reservations roll back with failed transactions and are released when a title is no longer used.
- Existing duplicate entries are preserved. Unchanged content can still be saved; new duplicate creation and conflicting renames are blocked. This permits deployment without deleting or inventing dictionary content.

Test-server audit on 2026-10-28 found one existing duplicate group: 「新诉」, entry IDs 7 and 23. Its editorial resolution is pending user review. No existing title or entry was changed automatically.

Verification: 288 backend tests (zero skipped), including migration over legacy duplicates, create/rename normalization, concurrent service/direct writes, and transaction rollback; 117 frontend tests and production build passed.
