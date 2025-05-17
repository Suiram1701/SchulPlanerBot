using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class ObjectsIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            #region Manually to let it still work
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."Guilds"
                SET "Notifications" = (
                    SELECT jsonb_agg(
                        jsonb_set(obj, '{ObjectsIn}', obj->'Between')
                    )
                    FROM jsonb_array_elements("Notifications") as obj
                )
                """);
            #endregion

            migrationBuilder.EnsureSchema("quartz");     // When calling DropSchema and the schema doesn't exists it throws
            migrationBuilder.DropSchema("quartz");     // Correction from last migration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            #region Manually
            CheckProvider();
            migrationBuilder.Sql("""
                UPDATE public."Guilds"
                SET "Notifications" = (
                    SELECT jsonb_agg(obj-'ObjectsIn')
                    FROM jsonb_array_elements("Notifications") as obj
                )
                """);
            #endregion
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
