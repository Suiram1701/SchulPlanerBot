using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class ModifyHomework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeworks_Guilds_OwnerId",
                table: "Homeworks");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "Homeworks",
                newName: "GuildId");

            migrationBuilder.RenameIndex(
                name: "IX_Homeworks_OwnerId",
                table: "Homeworks",
                newName: "IX_Homeworks_GuildId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedAt",
                table: "Homeworks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastModifiedBy",
                table: "Homeworks",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Homeworks_Guilds_GuildId",
                table: "Homeworks",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeworks_Guilds_GuildId",
                table: "Homeworks");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Homeworks");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Homeworks");

            migrationBuilder.RenameColumn(
                name: "GuildId",
                table: "Homeworks",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Homeworks_GuildId",
                table: "Homeworks",
                newName: "IX_Homeworks_OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeworks_Guilds_OwnerId",
                table: "Homeworks",
                column: "OwnerId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
