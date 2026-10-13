# Technical handoff corrections — 2026-10-13

Reviewed the implementation guide, relevant Entry Editor and Editorial Panel specifications, terminology, and the supplied HTML design source in `C:/Users/NODO/Desktop/technical/CHINESE.GE_Documents`. The scope is the outstanding screenshot corrections; this is not a claim that every legacy-migration or lexical-schema requirement has been re-audited.

## Completed in this pass

- G1–G5 now show the HTML design's titles and sources, individual collapse controls, Chinese text styling, preserved legacy bullets, highlighted searches, and a safe formatted/raw view. Untrusted HTML is never inserted into the live page.
- Comments show author and reply-recipient role labels. Names open a private-message composer, as the supplied HTML specifies. Replies notify their eligible author, preserving the GE notification exclusion.
- Comment entry links are persisted with offsets and entry IDs; entry renaming does not retarget them. Legacy comments without saved link metadata use unambiguous lookup. Updated and archived comments produce history and notification events.
- Inline abbreviation controls remove both text and annotation metadata. Ordinary text edits move or clear annotations. Definition and Georgian translation fields now render formatting during editing, preserve plain text storage, support plain-text paste and Undo/Redo, and keep the cursor bridge used by insertion tools.
- Chinese examples now have an image insertion control as well. Existing image context-anchor fallback behavior remains documented in `ep-photo-fixes.md`.
- Removed duplicate reference-material navigation and prevented the hidden Helper Materials form from appearing inside the reference tab.
- Pin/Unpin now uses the fixed side pane specified in the HTML rather than a sticky footer.
- History shows role, changed section, summary and before/after stages. Content snapshots include reference and media metadata; stage changes include lexical snapshots. Reference saves now have snapshots, audit events, notifications and entry-access checks.
- Editorial Work's second table is narrower and centered. Relevant marker/link/media/comment labels were made bilingual.
- Incomplete New Entry drafts can be saved with a warning. Missing POS/senses/definitions and duplicate POS groups block draft submission to review; draft creation remains permissive.

## Verification

- Frontend: 104 tests passed in 21 files.
- Backend: 282 tests passed, including temporary PostgreSQL integration databases.
- Production frontend and API compilation passed.
- Browser checks used an isolated mock API: reference formatting and collapse, comment entry link, name-to-private-message dialog, Pin/Unpin, formatted insertion, cursor movement, Undo, save and reload.
- Local database migration `20261013080000_AddCommentEntryLinks` applied.

## Remaining scope

1. Home versus All Entries product behavior is deliberately parked at the user's request; no new change to it was made in this pass.
2. The supplied folder contains design/specification files, not the deployed Chinese.ge public application source. Connecting inline media to that separate public site requires its actual code/integration point.
3. Production deployment and user acceptance with real editorial scenarios remain outstanding. Browser mock checks and regression tests do not replace that acceptance.
4. Restore/revert is explicitly excluded from this version by EP-HIST-002; older missing snapshots cannot be reconstructed.

This report supersedes the outstanding-item conclusions in `ep-second-audit.md`. In particular, the HTML implements reply addressing and name-to-private-message behavior; an independent recipient dropdown on every new comment is not required by that design.
