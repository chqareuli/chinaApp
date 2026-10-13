# EP screenshot fixes — 2026-10-08

Changes are implemented in the local project; production has not been deployed.

| Reference | Implemented changes |
| --- | --- |
| EP_01 | Chinese.ge favicon/logo on the login page and browser tab. |
| EP_02–04 | Default panel typography and navigation icons increased about 25%; bold names, role badges, distinct conversation presentation. Home excludes drafts; All Entries includes them. |
| EP_05 | Home and All Entries now have different draft scopes. |
| EP_06 | Centered statistics, colored counts, bold totals/export formats, section borders, distinct entry totals, links from editorial scores to entries, future export dates rejected by client and server. |
| EP_07 | Neutral heading and black blocked status distinct from gray cancelled status. |
| EP_08 | Tools above full-width definitions and equal example columns; smaller example badge; editable text retains its default size. |
| EP_09 | Stage tooltip, compact history, snapshots on content saves, maximum five pinyin and three traditional forms. |
| EP_10 | Homograph labels collapse after editing; marker symbols without prefixes; abbreviation and cross-reference insertion into text; safe italic/link rendering; bilingual media picker. |
| EP_11 | Comments can target a sense/example and reply to another comment; pin/font controls; G1–G5 reference search, filters, raw view, editing and collapse controls. |

## Validation

- Backend: 282 tests passed, including temporary PostgreSQL integration databases.
- Frontend: 95 tests passed across 18 files, including inline image persistence, cursor positions and formatted text rendering.
- Production frontend build and API build passed.
- Comment reply migration applied to the local development database.
- Editor layout and login logo inspected in the browser.

## Remaining input and limits

- Official Chinese.ge reference data imported from https://chinese.ge/#/abbreviations: 33 style, 95 domain, 15 POS, 12 abbreviation and 11 language entries. Existing custom codes are retained (local active totals: 34 style, 95 domain, 15 POS, 13 abbreviation, 12 language). Source values and apparent source typos are preserved; see chinese-ge-vocabularies.json.
- Historical saves made before snapshots existed cannot be reconstructed automatically. No restore UI is included.
- Inline images now capture the cursor position in a saved definition or Georgian example translation. Placement is stored with media and rendered between text runs after reload; editing shows a rendered preview. Legacy block media remains supported. Context anchors follow ordinary edits; ambiguous or fully replaced context falls back to the saved offset, clamped to text length. The shared public renderer accepts inline images, but this repository has no public dictionary API supplying authenticated media to the separate Chinese.ge deployment.
- Homographs now persist a pinyinId in their segment attributes. The editor can select an existing or newly added pronunciation; new selections resolve to a stable saved ID. Labels follow pronunciation renames and survive reordering. Missing/deleted linked pronunciations are rejected on save; unlink or reassign before deletion. Legacy independent labels remain available.
