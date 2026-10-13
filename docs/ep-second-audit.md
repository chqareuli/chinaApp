# Second screenshot audit — 2026-10-08

All eleven original images were reopened and compared with the current source. This is a source review supported by earlier browser checks, not a fresh end-to-end browser acceptance test for every screen.

| Photo | Implemented | Remaining or acceptance limit |
| --- | --- | --- |
| EP_01 | User-provided SVG login branding. | No additional functional gap found. |
| EP_02 | Larger global font/icons, bold names, role badges, unread activity polling, 25-item pagination. | Exact row-height preservation needs visual comparison with populated real data. Six total records correctly produce six rows, not ten fabricated rows. |
| EP_03 | Private/group threads and incoming/outgoing styling, linked message text, larger panel typography. | Verify a real two-editor reply scenario; source supports both directions. |
| EP_04 | Larger typography/icons and notification list/quick view. | No additional functional gap found from the annotation. |
| EP_05 | Home excludes drafts; All Entries includes them. | The photo asks why the screens duplicate, but does not prescribe that particular product distinction. This was an implementation assumption. |
| EP_06 | Status table centered/narrower, colors, totals, export date restrictions, format emphasis, dividers, distinct entry counts and score links. | Editorial Work table still lacks the narrower centered wrapper applied to the first table. |
| EP_07 | Neutral heading, black blocked and gray cancelled status. | No additional annotation-specific gap found. |
| EP_08 | Tools above fields, wide definitions, equal example columns, reduced example badge, unchanged default editable text size. | Dense chips/tools and long text still need acceptance testing at narrower widths. |
| EP_09 | Correct stage tooltip, compact history, form limits, content-save version snapshots. | Reference materials remain duplicated as a top-level tab and an article notes tab. History editor role labels are missing. Snapshots cover content saves; stage changes store status only, reference-material saves use separate audit records. Restore UI is explicitly a future feature in the annotation. |
| EP_10 | Pinyin-linked homograph labels, symbol-only style/domain chips, official vocabulary, text insertion for abbreviations/XR, inline images in editor definition/Georgian translation. | Inline abbreviations have no visible delete control (their chips are hidden); manual text deletion does not reconcile marker metadata. Italics only appear in rendered view, not the plain textarea. Some dialog/error strings remain Georgian-only. Images have no Chinese-example insertion control; public renderer support is not wired to a public dictionary API in this repo. Fully changed/ambiguous image anchor context falls back to saved offset. |
| EP_11 | Replies and sense/example comment targets, global font/pin controls, reference search/filter/edit/raw, overall collapse and colored groups, helper tab removed. | No independent recipient-editor selection for a new comment; replying alone does not fulfill that requirement. Comments render plain text, without linked entry references or role badges. G1–G5 lack individual collapse, source/language headings and the complete formatted reference presentation shown in the photo. |

## Recommended remaining order

1. EP_11: recipient addressing, comment entry links and role badges.
2. EP_10: abbreviation deletion and annotation reconciliation; bilingual dialog labels.
3. EP_11: per-group reference collapse and faithful source/format presentation.
4. EP_09: remove duplicate reference navigation, add history role labels, define the complete snapshot scope.
5. EP_06: narrow/center the Editorial Work table.
6. Populate real multi-editor acceptance scenarios and check typography, row heights and responsive layout.

The local project has not been deployed to the production server. Prior passing test totals (282 backend / 95 frontend) establish regression coverage, not completion of every screenshot requirement.
