using System;
using Chiniseapp.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_action", "created,status_changed,content_edited,role_changed,deactivated,archived,repair_mode_toggled,other")
                .Annotation("Npgsql:Enum:comment_status", "open,archived")
                .Annotation("Npgsql:Enum:editor_role", "super_admin,chief_editor,zh_editor,ka_editor,assistant_editor")
                .Annotation("Npgsql:Enum:entry_status", "new_entry,zh_review,ka_review,ready,published,archived")
                .Annotation("Npgsql:Enum:media_file_type", "image,video")
                .Annotation("Npgsql:Enum:placement", "inline,attached,standalone")
                .Annotation("Npgsql:Enum:score_type", "main,additional,ka_editor")
                .Annotation("Npgsql:Enum:segment_type", "homonym,gram_grp,pos,sense,definition,example,zh_segment,ka_segment,xr,style,domain,abbr,lang")
                .Annotation("Npgsql:Enum:vocabulary_category", "pos,style,domain,abbr,lang");

            migrationBuilder.CreateTable(
                name: "controlled_vocabularies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<VocabularyCategory>(type: "vocabulary_category", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_zh = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_ka = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_controlled_vocabularies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "editors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<EditorRole>(type: "editor_role", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_role_raw = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<AuditAction>(type: "audit_action", nullable: false),
                    performed_by_editor_id = table.Column<int>(type: "integer", nullable: false),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_log_entries_editors_performed_by_editor_id",
                        column: x => x.performed_by_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "direct_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sender_editor_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_editor_id = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_direct_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_direct_messages_editors_recipient_editor_id",
                        column: x => x.recipient_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_direct_messages_editors_sender_editor_id",
                        column: x => x.sender_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lemma = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pinyin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<EntryStatus>(type: "entry_status", nullable: false),
                    status_priority = table.Column<int>(type: "integer", nullable: false),
                    created_by_editor_id = table.Column<int>(type: "integer", nullable: false),
                    main_author_editor_id = table.Column<int>(type: "integer", nullable: true),
                    last_editor_editor_id = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    search_normalized_title = table.Column<string>(type: "text", nullable: false),
                    is_flagged_for_repair = table.Column<bool>(type: "boolean", nullable: false),
                    repair_notes = table.Column<string>(type: "text", nullable: true),
                    legacy_raw_comment_blob = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_entries_editors_created_by_editor_id",
                        column: x => x.created_by_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entries_editors_last_editor_editor_id",
                        column: x => x.last_editor_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entries_editors_main_author_editor_id",
                        column: x => x.main_author_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_editor_id = table.Column<int>(type: "integer", nullable: false),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    triggered_by_editor_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_editors_recipient_editor_id",
                        column: x => x.recipient_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_editors_triggered_by_editor_id",
                        column: x => x.triggered_by_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reference_materials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    g1 = table.Column<string>(type: "text", nullable: false),
                    g2 = table.Column<string>(type: "text", nullable: false),
                    g3 = table.Column<string>(type: "text", nullable: false),
                    g4 = table.Column<string>(type: "text", nullable: false),
                    g5 = table.Column<string>(type: "text", nullable: false),
                    original_raw_reference_material = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_materials", x => x.id);
                    table.ForeignKey(
                        name: "fk_reference_materials_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "score_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    editor_id = table.Column<int>(type: "integer", nullable: false),
                    score_type = table.Column<ScoreType>(type: "score_type", nullable: false),
                    awarded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_score_entries_editors_editor_id",
                        column: x => x.editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_score_entries_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "segments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    parent_segment_id = table.Column<int>(type: "integer", nullable: true),
                    segment_type = table.Column<SegmentType>(type: "segment_type", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: true),
                    value = table.Column<string>(type: "text", nullable: true),
                    content = table.Column<string>(type: "jsonb", nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: true),
                    placement = table.Column<Placement>(type: "placement", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_segments", x => x.id);
                    table.ForeignKey(
                        name: "fk_segments_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_segments_segments_parent_segment_id",
                        column: x => x.parent_segment_id,
                        principalTable: "segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    target_segment_id = table.Column<int>(type: "integer", nullable: true),
                    author_editor_id = table.Column<int>(type: "integer", nullable: false),
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<CommentStatus>(type: "comment_status", nullable: false),
                    original_raw_comment = table.Column<string>(type: "text", nullable: true),
                    parsed_comment_parts = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_comments_editors_author_editor_id",
                        column: x => x.author_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comments_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comments_segments_target_segment_id",
                        column: x => x.target_segment_id,
                        principalTable: "segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inserted_into_segment_id = table.Column<int>(type: "integer", nullable: true),
                    original_filename = table.Column<string>(type: "text", nullable: false),
                    safe_storage_name = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<MediaFileType>(type: "media_file_type", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_url = table.Column<string>(type: "text", nullable: false),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    placement = table.Column<string>(type: "text", nullable: true),
                    uploaded_by_editor_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_assets_editors_uploaded_by_editor_id",
                        column: x => x.uploaded_by_editor_id,
                        principalTable: "editors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_media_assets_segments_inserted_into_segment_id",
                        column: x => x.inserted_into_segment_id,
                        principalTable: "segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_entity_type_entity_id",
                table: "audit_log_entries",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_performed_by_editor_id",
                table: "audit_log_entries",
                column: "performed_by_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_editor_id",
                table: "comments",
                column: "author_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_entry_id",
                table: "comments",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_target_segment_id",
                table: "comments",
                column: "target_segment_id");

            migrationBuilder.CreateIndex(
                name: "ix_controlled_vocabularies_category_code",
                table: "controlled_vocabularies",
                columns: new[] { "category", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_direct_messages_recipient_editor_id_read_at_utc",
                table: "direct_messages",
                columns: new[] { "recipient_editor_id", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_direct_messages_sender_editor_id",
                table: "direct_messages",
                column: "sender_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_editors_email",
                table: "editors",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entries_created_by_editor_id",
                table: "entries",
                column: "created_by_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_last_editor_editor_id",
                table: "entries",
                column: "last_editor_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_main_author_editor_id",
                table: "entries",
                column: "main_author_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_search_normalized_title",
                table: "entries",
                column: "search_normalized_title")
                .Annotation("Npgsql:IndexMethod", "btree")
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_status_priority_updated_at_utc",
                table: "entries",
                columns: new[] { "status_priority", "updated_at_utc" },
                filter: "status <> 'new_entry'");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_inserted_into_segment_id",
                table: "media_assets",
                column: "inserted_into_segment_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_uploaded_by_editor_id",
                table: "media_assets",
                column: "uploaded_by_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_entry_id",
                table: "notifications",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_editor_id_is_read",
                table: "notifications",
                columns: new[] { "recipient_editor_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_triggered_by_editor_id",
                table: "notifications",
                column: "triggered_by_editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_reference_materials_entry_id",
                table: "reference_materials",
                column: "entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_entries_editor_id",
                table: "score_entries",
                column: "editor_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_entries_entry_id_editor_id_score_type",
                table: "score_entries",
                columns: new[] { "entry_id", "editor_id", "score_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_segments_entry_id",
                table: "segments",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_segments_entry_id_parent_segment_id_order_index",
                table: "segments",
                columns: new[] { "entry_id", "parent_segment_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_segments_parent_segment_id",
                table: "segments",
                column: "parent_segment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "controlled_vocabularies");

            migrationBuilder.DropTable(
                name: "direct_messages");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "reference_materials");

            migrationBuilder.DropTable(
                name: "score_entries");

            migrationBuilder.DropTable(
                name: "segments");

            migrationBuilder.DropTable(
                name: "entries");

            migrationBuilder.DropTable(
                name: "editors");
        }
    }
}
