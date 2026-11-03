# Export version 3 retest

Implementation and verification status as of 3 November 2026. This checklist does not assert that the running test server has been updated.

## Implemented

- R01: prevent deletion/replacement of media-linked nodes, serialize media insertion and content deletion on the entry lock, retain historical missing-target warnings, and provide explicit image reattachment without changing link/file identity.
- R02: put boundary images outside lexical links, including grouped links. Tests include the reported sense 917 / definition 918 / XR 920 / image 21 at offset 102.
- R03: preserve a custom homograph title with an independent pronunciation link (`labelWithPinyin`). Five labels sharing one pronunciation and changing a pronunciation without replacing a title are covered.
- R04: one XR owns an ordered target list with stable UUIDs. Tests cover 2–5 targets, edit/removal/reordering, JSON input, save/reload and JSON/XML export. Existing source prefixes, separators and suffixes survive an unchanged dialog save.
- Observe route parameter changes and discard previous-entry responses; display inherited markers without storing copies; show new comment topics first with replies inside their topic; use R1–R5 display labels with unchanged storage keys; use English–Chinese publication warnings.
- Ready policy confirmed by the owner: allow incomplete entries. Publication checks stay on Published and already-published content saves.

## Verification completed

- 336 .NET tests passed, including all PostgreSQL integration tests against an isolated temporary cluster.
- 130 Angular tests passed across 24 files.
- Angular production build passed.
- 21 generated JSON exports passed project JSON Schema 3 validation.
- XML serializer output passed the embedded project XSD checks exercised by the regression tests.
- Existing application database and source report exports were not rewritten.

## Retest after coordinated backend and frontend deployment

1. Repeat the original 12-article exports. Compare component IDs, order, text, media identities and warnings. Download one article separately and compare its entry subtree with the bulk export.
2. Confirm image 21 is after XR 920, outside its `ref` and `xr`. Test images before/after a link and between adjacent links.
3. In entry 2, inspect links 12 → 138, 14 → 119, 15 → 131, and 16 → 119. An editor must identify each correct destination. Use **Move / reattach**, position the cursor in that saved definition/example, then confirm through its image button. Link IDs and shared-file IDs must remain unchanged. Do not guess destinations from similar text.
4. For entry 33, preserve each `介` title while selecting `jiè` as its pronunciation. Test a second pronunciation and confirm title and block ID remain unchanged.
5. Create one grouped link with 2, 3, 4 and 5 targets. Each target must open separately; editing/removing/reordering one must preserve the other IDs and the shared label. Compare JSON and XML.
6. Follow a link between entries without reloading the browser. URL, entry number, forms, notes and media must all refer to the newly opened entry, including when the preceding request finishes late.
7. Confirm incomplete entries can remain Ready. Confirm missing Pinyin/POS/definition-or-XR prevents Published, and successful saves clear the old unsaved-changes warning.

## Separate acceptance work

The production RAW parser/import pipeline is not present in this repository. Its target grouping and RAW → database → editor → export pilot still need integration in that codebase. Project JSON Schema/XSD files are available; full TEI customization and editorial profile acceptance are not certified by these tests. Historical media destinations and a full dictionary migration require separate confirmation.
