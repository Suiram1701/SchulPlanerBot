using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Diagnostics;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class MultipleNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notifications",
                table: "Guilds",
                type: "jsonb",
                nullable: true);

            #region Manually written to prevent a loss of data
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."Guilds"
                SET "Notifications" = 
                    CASE
                        WHEN "NotificationsEnabled" = TRUE THEN
                            jsonb_build_array(
                                jsonb_build_object(
                                    'StartAt', "StartNotifications",
                                    'Between', TO_CHAR("BetweenNotifications", 'DD HH24:MI:SS'),
                                    'ChannelId', "ChannelId"
                                )
                            )
                        ELSE
                            '[]'::jsonb
                    END
                """);
            #endregion

            migrationBuilder.DropColumn(
                name: "BetweenNotifications",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "StartNotifications",
                table: "Guilds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "BetweenNotifications",
                table: "Guilds",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChannelId",
                table: "Guilds",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "Guilds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartNotifications",
                table: "Guilds",
                type: "timestamp with time zone",
                nullable: true);

            #region Manually written to minimize the loss of data
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."Guilds"
                SET 
                    "NotificationsEnabled" = jsonb_array_length("Notifications") > 0
                    "StartNotifications" = 
                        CASE 
                            WHEN "NotificationsEnabled"
                            THEN ("Notifications"->0->>'StartAt')::TIMESTAMP WITH TIME ZONE
                            ELSE NULL 
                        END,
                    "BetweenNotifications" = 
                        CASE 
                            WHEN "NotificationsEnabled"
                            THEN ("Notifications"->0->>'Between')::INTERVAL
                            ELSE NULL 
                        END,
                    "ChannelId" = 
                        CASE 
                            WHEN "NotificationsEnabled"
                            THEN ("Notifications"->0->>'ChannelId')::BIGINT 
                            ELSE NULL 
                        END
                """);
            #endregion

            migrationBuilder.DropColumn(
                name: "Notifications",
                table: "Guilds");
        }

        private bool CheckProvider()
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                Debug.WriteLine($"Warning: Executing migration with provider {ActiveProvider} while SQL update is made for PostgreSQL!");
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
