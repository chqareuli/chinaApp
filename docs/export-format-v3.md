# Dictionary export version 3

The export endpoint keeps the outer JSON array and existing JSON/XML download names. Each JSON entry now declares `schemaVersion: 3`. XML identifies the profile in `teiHeader/fileDesc/editionStmt/edition`. These are dictionary interchange records, separate from the editor GET/PUT contract.

## Hierarchy and pronunciation

`homonyms[].gramGroups[].senses` and `homonyms[].directSenses` are the canonical paths. The redundant homonym `senses` summary is omitted from exports; it remains available to legacy editor clients. Root grammar groups, direct POS groups and senses retain their existing paths. No extra grammar groups are introduced for export.

Homonyms have `labelMode: "pinyin"` when they hold an explicit `pinyinId`, otherwise `"independent"`. Unlinked legacy labels are not guessed to be pronunciations. XML uses a linked `form/pron/@corresp` for the first case and `label[@type='independent']` for the second. Form IDs in XML are scoped to the entry, since pronunciation/traditional-form IDs are entry-local.

## Text and annotations

Version 3 adds `labelMode: "labelWithPinyin"`: `text` remains the editor's chosen title while `pinyinId` links independently to a pronunciation. XML emits both `label[@type='labelWithPinyin']` and `form/pron/@corresp`. Existing `pinyin` labels still follow pronunciation edits, and existing independent labels remain unlinked. Several homographs may share the same pronunciation. No label is inferred from its script or spelling.

Cross-references optionally contain an ordered `targets` list. Each target has a stable UUID `id`, textual `target`, optional resolved entry identity, `displayText`, and a `textOffset` relative to the XR's full `displayText`. The outer XR offset remains relative to its containing field. Export adds target `textLength`. The shared label and separators live once in the XR display string; targets mark independently clickable substrings. XML uses one `xr` with ordered `ref` children, each with `xml:id="xr-target-{id}"`. Editing, deleting or reordering targets preserves all surviving target IDs. The legacy scalar target fields mirror the first target. Legacy single-target records without `targets` remain readable. An older client cannot overwrite a persisted target list with scalar fields only.

Targets are structured values stored in the XR segment's JSON attributes, not new segment rows. This version requires no relational schema migration. Deploy backend and frontend together. Older backend versions reject the new strict DTO attributes; rolling back after new writes requires a compatible backend or restoring the deployment's data snapshot, not silently removing grouped target data.

Offsets are zero-based UTF-16 code units, measured against the exact decoded field string. The interval is `[textOffset, textOffset + textLength)`. No export-time Unicode normalization is applied. This matches JavaScript string indices and .NET string indexing. JSON reference and inline marker records include `textLength`.

`definitionKind` distinguishes `definition`, `crossReferenceOnly` and `empty`. A reference-only sense has `definition: null`; its original text remains in `displayText`. Its inline annotations then refer to `textField: "displayText"`. Render the display text with inline annotations once, adding attached references separately. An attached reference with an empty display field still carries its own reference display text.

XML embeds `xr/ref` and `abbr` in the owning `def` or example `quote` at their exact positions, replacing the corresponding plain-text span. Reference-only content uses `seg[@type='crossReferenceOnly']` instead of `def`. Definition and example segment IDs are retained. Multiline fields use `xml:space="preserve"`. Attached style/domain markers remain on their owner in their saved order.

When saving edited text, an annotation whose saved offset no longer matches can be relocated only when its exact display text occurs once. Missing/ambiguous positions require the annotation to be reinserted. Both export formats reject invalid or overlapping positions with a validation message instead of silently discarding annotation locations.

## Media

Each JSON entry includes `media`. The link `id` identifies a use; `mediaId` identifies the shared stored asset, including when reused by multiple entries. Each record retains its URL, MIME type, name, original placement and parent segment. Inline `position` contains the resolved node, field and offset. For reference-only senses it refers to `displayText`.

XML uses `figure/graphic`, with `idno[@type='media']` for shared identity and `figure/@corresp` for the parent. Inline figures are inserted at their resolved text position. Original placement metadata is retained in `note[@type='placement']`; non-inline uses are retained under the entry with their parent reference. Context-based media positioning follows the editor's existing resolution rule after text edits.

The controller makes local media URLs absolute. These remain authenticated application endpoints; file binaries are not bundled. An importer needs access to the source host and credentials. A self-contained migration package and media-file copying are separate work.

If an inline media target was deleted or recreated, the download still includes the asset at entry level. JSON preserves `parentSegmentId` and the original `placement`, sets resolved `position` to null, and adds a `missingMediaTarget` item to `exportWarnings` with entry, media and target IDs. XML retains the figure at entry level with `type="unresolvedPlacement"`, original placement and an `exportWarning` note; it does not emit a dangling `corresp`. Export does not guess a new parent or rewrite the database. The editor must reattach the image to restore its inline position.

Media at the beginning or end of an inline annotation is emitted outside that annotation, under the owning text field. At the shared boundary between two links it appears between them. Media strictly inside an annotation retains that position.

Content saves reject removal or replacement of a media-linked segment until its media link is reattached or removed. The error identifies the entry, media link and target segment. Both the relational parent and current/legacy placement metadata are checked. Reordering or editing a surviving segment is permitted. Content saves and image upload/reuse serialize on the entry row while checking target existence, preventing a concurrent insertion from leaving a link to a newly deleted segment. This does not modify or repair historical missing-target records or delete their underlying image files.

## Publication and verification

The media tab provides **Move / reattach**. Select the existing image, put the cursor at its confirmed destination in a saved definition/example, then use that field's image button and confirm. `PUT /media/{id}/placement` updates only that link's parent and placement, retains the same media ID and file, enforces editing permissions and validates the target under the entry lock. Historical missing-target records are not automatically reassigned.

The editor observes route parameter changes, cancels previous-entry requests and clears entry-specific dialogs/data. Reference group storage keys remain `g1`–`g5`; the visible labels are `R1`–`R5`. Comment topics are newest first, with replies ordered chronologically inside their original topic. Inherited style/domain markers are derived for display, with no child copies stored.

Schema files:

- `docs/schemas/chinese-ge-export-v3.schema.json` (JSON Schema draft 7).
- `docs/schemas/chinese-ge-export-v3.xsd` (project XML profile using TEI element names).

JSON sample validation after `npm ci`, from `frontend/editorial-panel`:

```text
node tools/validate-export-schema.cjs <directory-of-json-exports>
```

Set `CHINESE_EXPORT_SAMPLES` to a temporary directory when running .NET tests to generate JSON/XML regression exports. JSON Schema validates shape and basic constraints; server rules separately validate target ranges, identity ownership, pronunciation links and annotation positions. The repository does not contain the production RAW parser/import pipeline, so its grouped-target integration and full-dictionary migration are not certified by these editor/export tests.

The Pinyin limit is six on both client and server. Publishing, and saving an already published entry, requires 1–6 distinct nonempty Pinyin values. Empty definitions are permitted when a sense has a reference with a positive target entry ID or nonempty textual target. Target existence/resolution across the full dictionary is not certified by this check. Example-only and empty senses remain incomplete. On 3 November 2026 the owner confirmed that incomplete entries remain allowed in Ready; the publication checks must not be added to that transition. The existing NewEntry-to-ZhReview checks remain unchanged.

The export controller reads entry content and media within a PostgreSQL repeatable-read snapshot. Individual and bulk exports use the same serializer.

Regression tests cover Unicode, repeated link text, abbreviation placement, reference-only senses, media reuse/positioning, hierarchy, stable segment IDs and pronunciation limits. Version 3 includes a JSON Schema and a project XML XSD. Every XML download is validated against the embedded project profile. Generated regression exports are also checked against JSON Schema using the validation tool above. Editorial profile agreement, the supplied article corpus on the deployed version, and a RAW migration pilot remain acceptance work; this XSD is not the full TEI schema or an editorially approved ODD customization.
