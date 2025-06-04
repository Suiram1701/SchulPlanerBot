using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

namespace SchulPlanerBot.Migrations
{
    /// <inheritdoc />
    public partial class V0_3_0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qrtz_blob_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_calendars",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_cron_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_fired_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_locks",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_paused_trigger_grps",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_scheduler_state",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_simple_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_simprop_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_job_details",
                schema: "quartz");
            
            migrationBuilder.DropSchema("quartz");

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
            migrationBuilder.Sql("""
                UPDATE public."HomeworkSubscriptions"
                    SET "Include" = "Include" || ARRAY[NULL::TEXT]
                WHERE "NoSubject" = TRUE;
                """);
            #endregion

            migrationBuilder.DropColumn(
                name: "NoSubject",
                table: "HomeworkSubscriptions");

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
            migrationBuilder.AddColumn<bool>(
                name: "NoSubject",
                table: "HomeworkSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
            migrationBuilder.Sql("""
                UPDATE public."HomeworkSubscriptions"
                SET
                    "NoSubject" = TRUE,
                    "Include" = array_remove("Include", NULL::TEXT)
                WHERE array_position("Include", NULL::TEXT) != -1
                """);
            #endregion

            migrationBuilder.AlterColumn<string[]>(
                name: "Include",
                table: "HomeworkSubscriptions",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(HashSet<string>),
                oldType: "text[]",
                oldDefaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.DropColumn(
                name: "Exclude",
                table: "HomeworkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Notifications",
                table: "Guilds");

            migrationBuilder.EnsureSchema(
                name: "quartz");

            migrationBuilder.CreateTable(
                name: "qrtz_calendars",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    calendar_name = table.Column<string>(type: "text", nullable: false),
                    calendar = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_calendars", x => new { x.sched_name, x.calendar_name });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_fired_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    entry_id = table.Column<string>(type: "text", nullable: false),
                    fired_time = table.Column<long>(type: "bigint", nullable: false),
                    instance_name = table.Column<string>(type: "text", nullable: false),
                    is_nonconcurrent = table.Column<bool>(type: "bool", nullable: false),
                    job_group = table.Column<string>(type: "text", nullable: true),
                    job_name = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    requests_recovery = table.Column<bool>(type: "bool", nullable: true),
                    sched_time = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_fired_triggers", x => new { x.sched_name, x.entry_id });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_job_details",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    job_group = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_durable = table.Column<bool>(type: "bool", nullable: false),
                    is_nonconcurrent = table.Column<bool>(type: "bool", nullable: false),
                    is_update_data = table.Column<bool>(type: "bool", nullable: false),
                    job_class_name = table.Column<string>(type: "text", nullable: false),
                    job_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    requests_recovery = table.Column<bool>(type: "bool", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_job_details", x => new { x.sched_name, x.job_name, x.job_group });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_locks",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    lock_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_locks", x => new { x.sched_name, x.lock_name });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_paused_trigger_grps",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_paused_trigger_grps", x => new { x.sched_name, x.trigger_group });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_scheduler_state",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    instance_name = table.Column<string>(type: "text", nullable: false),
                    checkin_interval = table.Column<long>(type: "bigint", nullable: false),
                    last_checkin_time = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_scheduler_state", x => new { x.sched_name, x.instance_name });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    job_group = table.Column<string>(type: "text", nullable: false),
                    calendar_name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    end_time = table.Column<long>(type: "bigint", nullable: true),
                    job_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    misfire_instr = table.Column<short>(type: "smallint", nullable: true),
                    next_fire_time = table.Column<long>(type: "bigint", nullable: true),
                    prev_fire_time = table.Column<long>(type: "bigint", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<long>(type: "bigint", nullable: false),
                    trigger_state = table.Column<string>(type: "text", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_triggers_qrtz_job_details_sched_name_job_name_job_group",
                        columns: x => new { x.sched_name, x.job_name, x.job_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_job_details",
                        principalColumns: new[] { "sched_name", "job_name", "job_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_blob_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    blob_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_blob_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_blob_triggers_qrtz_triggers_sched_name_trigger_name_tr~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_cron_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    time_zone_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_cron_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_cron_triggers_qrtz_triggers_sched_name_trigger_name_tr~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_simple_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    repeat_count = table.Column<long>(type: "bigint", nullable: false),
                    repeat_interval = table.Column<long>(type: "bigint", nullable: false),
                    times_triggered = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_simple_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_simple_triggers_qrtz_triggers_sched_name_trigger_name_~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_simprop_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    bool_prop_1 = table.Column<bool>(type: "bool", nullable: true),
                    bool_prop_2 = table.Column<bool>(type: "bool", nullable: true),
                    dec_prop_1 = table.Column<decimal>(type: "numeric", nullable: true),
                    dec_prop_2 = table.Column<decimal>(type: "numeric", nullable: true),
                    int_prop_1 = table.Column<int>(type: "integer", nullable: true),
                    int_prop_2 = table.Column<int>(type: "integer", nullable: true),
                    long_prop_1 = table.Column<long>(type: "bigint", nullable: true),
                    long_prop_2 = table.Column<long>(type: "bigint", nullable: true),
                    str_prop_1 = table.Column<string>(type: "text", nullable: true),
                    str_prop_2 = table.Column<string>(type: "text", nullable: true),
                    str_prop_3 = table.Column<string>(type: "text", nullable: true),
                    time_zone_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_simprop_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_simprop_triggers_qrtz_triggers_sched_name_trigger_name~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_group",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "job_group");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "job_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_req_recovery",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "requests_recovery");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_group",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "trigger_group");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_inst_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "instance_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "trigger_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_nm_gp",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                columns: new[] { "sched_name", "trigger_name", "trigger_group" });

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_j_req_recovery",
                schema: "quartz",
                table: "qrtz_job_details",
                column: "requests_recovery");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_next_fire_time",
                schema: "quartz",
                table: "qrtz_triggers",
                column: "next_fire_time");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_nft_st",
                schema: "quartz",
                table: "qrtz_triggers",
                columns: new[] { "next_fire_time", "trigger_state" });

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_state",
                schema: "quartz",
                table: "qrtz_triggers",
                column: "trigger_state");

            migrationBuilder.CreateIndex(
                name: "IX_qrtz_triggers_sched_name_job_name_job_group",
                schema: "quartz",
                table: "qrtz_triggers",
                columns: new[] { "sched_name", "job_name", "job_group" });
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
