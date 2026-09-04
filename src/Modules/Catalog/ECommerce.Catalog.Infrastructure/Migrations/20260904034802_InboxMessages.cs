using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "catalog",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_message", x => new { x.message_id, x.consumer });
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_message_received_at",
                schema: "catalog",
                table: "inbox_message",
                column: "received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "catalog");
        }
    }
}
