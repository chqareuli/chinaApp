using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentAddedAuditAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_action", "created,status_changed,content_edited,role_changed,deactivated,archived,repair_mode_toggled,comment_added,other")
                .Annotation("Npgsql:Enum:comment_status", "open,archived")
                .Annotation("Npgsql:Enum:editor_role", "super_admin,chief_editor,zh_editor,ka_editor,assistant_editor")
                .Annotation("Npgsql:Enum:entry_status", "new_entry,zh_review,ka_review,ready,published,archived")
                .Annotation("Npgsql:Enum:media_file_type", "image,video")
                .Annotation("Npgsql:Enum:placement", "inline,attached,standalone")
                .Annotation("Npgsql:Enum:score_type", "main,additional,ka_editor")
                .Annotation("Npgsql:Enum:segment_type", "homonym,gram_grp,pos,sense,definition,example,zh_segment,ka_segment,xr,style,domain,abbr,lang")
                .Annotation("Npgsql:Enum:vocabulary_category", "pos,style,domain,abbr,lang")
                .OldAnnotation("Npgsql:Enum:audit_action", "created,status_changed,content_edited,role_changed,deactivated,archived,repair_mode_toggled,other")
                .OldAnnotation("Npgsql:Enum:comment_status", "open,archived")
                .OldAnnotation("Npgsql:Enum:editor_role", "super_admin,chief_editor,zh_editor,ka_editor,assistant_editor")
                .OldAnnotation("Npgsql:Enum:entry_status", "new_entry,zh_review,ka_review,ready,published,archived")
                .OldAnnotation("Npgsql:Enum:media_file_type", "image,video")
                .OldAnnotation("Npgsql:Enum:placement", "inline,attached,standalone")
                .OldAnnotation("Npgsql:Enum:score_type", "main,additional,ka_editor")
                .OldAnnotation("Npgsql:Enum:segment_type", "homonym,gram_grp,pos,sense,definition,example,zh_segment,ka_segment,xr,style,domain,abbr,lang")
                .OldAnnotation("Npgsql:Enum:vocabulary_category", "pos,style,domain,abbr,lang");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                .Annotation("Npgsql:Enum:vocabulary_category", "pos,style,domain,abbr,lang")
                .OldAnnotation("Npgsql:Enum:audit_action", "created,status_changed,content_edited,role_changed,deactivated,archived,repair_mode_toggled,comment_added,other")
                .OldAnnotation("Npgsql:Enum:comment_status", "open,archived")
                .OldAnnotation("Npgsql:Enum:editor_role", "super_admin,chief_editor,zh_editor,ka_editor,assistant_editor")
                .OldAnnotation("Npgsql:Enum:entry_status", "new_entry,zh_review,ka_review,ready,published,archived")
                .OldAnnotation("Npgsql:Enum:media_file_type", "image,video")
                .OldAnnotation("Npgsql:Enum:placement", "inline,attached,standalone")
                .OldAnnotation("Npgsql:Enum:score_type", "main,additional,ka_editor")
                .OldAnnotation("Npgsql:Enum:segment_type", "homonym,gram_grp,pos,sense,definition,example,zh_segment,ka_segment,xr,style,domain,abbr,lang")
                .OldAnnotation("Npgsql:Enum:vocabulary_category", "pos,style,domain,abbr,lang");
        }
    }
}
