using Microsoft.EntityFrameworkCore.Migrations;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            #region Manually written to prevent a loss of data
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."HomeworkSubscriptions"
                    SET "Include" = "Include" || ARRAY[NULL::TEXT]
                WHERE "NoSubject" = TRUE;
                """);
            #endregion

            migrationBuilder.DropColumn(
                name: "NoSubject",
                table: "HomeworkSubscriptions");

            migrationBuilder.AlterColumn<HashSet<string>>(
                name: "Include",
                table: "HomeworkSubscriptions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]",
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AddColumn<HashSet<string>>(
                name: "Exclude",
                table: "HomeworkSubscriptions",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            #region Manually written to prevent a loss of data
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."HomeworkSubscriptions"
                SET
                    "NoSubject" = TRUE,
                    "Include" = array_remove("Include", NULL::TEXT)
                WHERE array_position("Include", NULL::TEXT) != -1
                """);
            #endregion

            migrationBuilder.DropColumn(
                name: "Exclude",
                table: "HomeworkSubscriptions");

            migrationBuilder.AlterColumn<string[]>(
                name: "Include",
                table: "HomeworkSubscriptions",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(HashSet<string>),
                oldType: "text[]",
                oldDefaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<bool>(
                name: "NoSubject",
                table: "HomeworkSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
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
