using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorTeamProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chinese_name",
                table: "editors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "editorial_role",
                table: "editors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "editors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cancelled",
                table: "editors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                table: "editors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "we_chat_account",
                table: "editors",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chinese_name",
                table: "editors");

            migrationBuilder.DropColumn(
                name: "editorial_role",
                table: "editors");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "editors");

            migrationBuilder.DropColumn(
                name: "is_cancelled",
                table: "editors");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "editors");

            migrationBuilder.DropColumn(
                name: "we_chat_account",
                table: "editors");
        }
    }
}
