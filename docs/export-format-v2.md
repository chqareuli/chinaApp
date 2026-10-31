# Dictionary export version 2

The export endpoint keeps the outer JSON array and existing JSON/XML download names. Each JSON entry now declares `schemaVersion: 2`. XML identifies the profile in `teiHeader/fileDesc/editionStmt/edition`. These are dictionary interchange records, separate from the editor GET/PUT contract.

## Hierarchy and pronunciation

`homonyms[].gramGroups[].senses` and `homonyms[].directSenses` are the canonical paths. The redundant homonym `senses` summary is omitted from exports; it remains available to legacy editor clients. Root grammar groups, direct POS groups and senses retain their existing paths. No extra grammar groups are introduced for export.

Homonyms have `labelMode: "pinyin"` when they hold an explicit `pinyinId`, otherwise `"independent"`. Unlinked legacy labels are not guessed to be pronunciations. XML uses a linked `form/pron/@corresp` for the first case and `label[@type='independent']` for the second. Form IDs in XML are scoped to the entry, since pronunciation/traditional-form IDs are entry-local.

## Text and annotations

Offsets are zero-based UTF-16 code units, measured against the exact decoded field string. The interval is `[textOffset, textOffset + textLength)`. No export-time Unicode normalization is applied. This matches JavaScript string indices and .NET string indexing. JSON reference and inline marker records include `textLength`.

`definitionKind` distinguishes `definition`, `crossReferenceOnly` and `empty`. A reference-only sense has `definition: null`; its original text remains in `displayText`. Its inline annotations then refer to `textField: "displayText"`. Render the display text with inline annotations once, adding attached references separately. An attached reference with an empty display field still carries its own reference display text.

XML embeds `xr/ref` and `abbr` in the owning `def` or example `quote` at their exact positions, replacing the corresponding plain-text span. Reference-only content uses `seg[@type='crossReferenceOnly']` instead of `def`. Definition and example segment IDs are retained. Multiline fields use `xml:space="preserve"`. Attached style/domain markers remain on their owner in their saved order.

When saving edited text, an annotation whose saved offset no longer matches can be relocated only when its exact display text occurs once. Missing/ambiguous positions require the annotation to be reinserted. Both export formats reject invalid or overlapping positions with a validation message instead of silently discarding annotation locations.

## Media

Each JSON entry includes `media`. The link `id` identifies a use; `mediaId` identifies the shared stored asset, including when reused by multiple entries. Each record retains its URL, MIME type, name, original placement and parent segment. Inline `position` contains the resolved node, field and offset. For reference-only senses it refers to `displayText`.

XML uses `figure/graphic`, with `idno[@type='media']` for shared identity and `figure/@corresp` for the parent. Inline figures are inserted at their resolved text position. Original placement metadata is retained in `note[@type='placement']`; non-inline uses are retained under the entry with their parent reference. Context-based media positioning follows the editor's existing resolution rule after text edits.

The controller makes local media URLs absolute. These remain authenticated application endpoints; file binaries are not bundled. An importer needs access to the source host and credentials. A self-contained migration package and media-file copying are separate work.

## Publication and verification

The Pinyin limit is six on both client and server. Publishing, and saving an already published entry, requires 1–6 distinct nonempty Pinyin values. Empty definitions are permitted when a sense has a reference with a positive target entry ID or nonempty textual target. Target existence/resolution across the full dictionary is not certified by this check. Example-only and empty senses remain incomplete. Ready-stage rules are unchanged pending editorial agreement.

The export controller reads entry content and media within a PostgreSQL repeatable-read snapshot. Individual and bulk exports use the same serializer.

Regression tests cover Unicode, repeated link text, abbreviation placement, reference-only senses, media reuse/positioning, hierarchy, stable segment IDs and pronunciation limits. This version has not been validated against an editorially agreed JSON Schema or TEI ODD/Relax NG profile. Profile agreement and formal schema validation, a rerun of the supplied 12-article export corpus, and a migration pilot remain required acceptance work.
