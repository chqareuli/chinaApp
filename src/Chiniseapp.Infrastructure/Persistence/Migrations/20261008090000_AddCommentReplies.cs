using Microsoft.EntityFrameworkCore.Migrations;

namespace Chiniseapp.Infrastructure.Persistence.Migrations;

public partial class AddCommentReplies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "reply_to_comment_id", table: "comments", type: "integer", nullable: true);
        migrationBuilder.CreateIndex(name: "ix_comments_reply_to_comment_id", table: "comments", column: "reply_to_comment_id");
        migrationBuilder.AddForeignKey(name: "fk_comments_comments_reply_to_comment_id", table: "comments", column: "reply_to_comment_id", principalTable: "comments", principalColumn: "id", onDelete: ReferentialAction.SetNull);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_comments_comments_reply_to_comment_id", table: "comments");
        migrationBuilder.DropIndex(name: "ix_comments_reply_to_comment_id", table: "comments");
        migrationBuilder.DropColumn(name: "reply_to_comment_id", table: "comments");
    }
}
