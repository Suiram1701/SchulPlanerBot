using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class CronNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notifications",
                table: "Guilds");

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ObjectsIn = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => new { x.GuildId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_Notification_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.AddColumn<string>(
                name: "Notifications",
                table: "Guilds",
                type: "jsonb",
                nullable: true);
        }
    }
}
