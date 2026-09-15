using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chiniseapp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastMessagesAndNotificationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detail",
                table: "notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "broadcast_id",
                table: "direct_messages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "detail",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "broadcast_id",
                table: "direct_messages");
        }
    }
}
