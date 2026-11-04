# Entry 2: obsolete media links

Reviewed snapshots: Desktop/china/files/chinese-ge (JSON_标测_SARA).json and
chinese-ge (XML_标测_SARA).xml. Both show four unresolved old placements:

| Image | Link ID | Missing segment |
| --- | --- | --- |
| SARA | 12 | 138 |
| IKIGAI | 14 | 119 |
| ტაძარი | 15 | 131 |
| 汤哥 | 16 | 119 |

The active SARA link 22 targets segment 811; XML places it in definition 814.
Links 12 and 22 share the same stored image. The snapshots do not establish
the original intended positions of the four obsolete links. Recovering those
positions requires historical revisions/backups or editorial confirmation.

The old database relationship uses `ON DELETE SET NULL`: deleting a target can
clear the relational segment ID while leaving the old target in the placement
string. This explains the orphan representation, but the snapshots cannot prove
which historical operation deleted/replaced the original segments.

## Cleanup after deploying this change

Open entry 2's media list. For IDs 12, 14, 15 and 16, use **Remove obsolete link;
keep image**, then confirm. Do not use permanent Delete. Leave link 22 alone.
Export again: those four placements and their warnings should be absent; the
active SARA placement should remain. The images remain available in the library.

`POST /media/{id}/detach-stale-link` verifies edit permission, locks the entry,
and rejects any link with a surviving target or unspecified placement. It keeps
the media row, owner and file, stores the original placement in a reserved
`detached:` envelope and records an audit event. Detached rows are excluded from
article media lists and exports, but remain in the library for reuse. Repeating
the operation is safe. No schema migration or automatic production cleanup occurs.

Current save protection rejects deletion/replacement of a media-linked segment:
reattach its image first. Ready may remain incomplete; Published validation policy
is unchanged. Existing orphans require the explicit cleanup above.

Validation: 338 backend tests (including real PostgreSQL cleanup, file preservation,
permissions, active-link rejection and reuse), 131 frontend tests, production build,
and 37 generated JSON exports checked against the project schema.

See [title + pronunciation example](examples/entry-33-label-pinyin/README.md).
Production data has not been changed by this local implementation or these tests.
