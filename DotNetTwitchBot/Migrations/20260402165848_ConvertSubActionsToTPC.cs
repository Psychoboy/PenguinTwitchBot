using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSubActionsToTPC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Backup existing data
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE SubActions_Backup AS 
                SELECT 
                    Id,
                    `Index`,
                    Text,
                    `File`,
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    UseBot,
                    FallBack,
                    StreamOnly,
                    Duration,
                    Volume,
                    CSS,
                    HttpMethod,
                    Headers,
                    Min,
                    Max,
                    Append
                FROM SubActions;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_SubActions_Actions_ActionTypeId",
                table: "SubActions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubActions",
                table: "SubActions");

            // Drop the old table
            migrationBuilder.DropTable(name: "SubActions");

            // Create new tables for TPC
            migrationBuilder.CreateTable(
                name: "SubActions_Alert",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    Volume = table.Column<float>(type: "float", nullable: false),
                    CSS = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_Alert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_Alert_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_CurrentTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_CurrentTime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_CurrentTime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_ExternalApi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    HttpMethod = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Headers = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_ExternalApi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_ExternalApi_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_FollowAge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_FollowAge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_FollowAge_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_PlaySound",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_PlaySound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_PlaySound_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_RandomInt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    Min = table.Column<int>(type: "int", nullable: false),
                    Max = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_RandomInt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_RandomInt_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_SendMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    UseBot = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FallBack = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StreamOnly = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_SendMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_SendMessage_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_Uptime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_Uptime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_Uptime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_WatchTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_WatchTime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_WatchTime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubActions_WriteFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubActionTypes = table.Column<int>(type: "int", nullable: false),
                    ActionTypeId = table.Column<int>(type: "int", nullable: true),
                    Append = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubActions_WriteFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubActions_WriteFile_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Step 2: Migrate data from backup to new tables with sequential Int IDs
            // Note: SubActionTypes enum values:
            // 1 = SendMessage, 2 = Alert, 3 = PlaySound, 4 = WriteFile, 5 = RandomInt,
            // 6 = CurrentTime, 7 = Followage, 8 = Uptime, 9 = ExternalApi, 10 = WatchTime

            migrationBuilder.Sql(@"
                SET @row_num = 0;

                -- Migrate SendMessage (SubActionTypes = 1 or 11 for ReplyToMessage)
                INSERT INTO SubActions_SendMessage (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId, UseBot, FallBack, StreamOnly)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    COALESCE(UseBot, 1),
                    COALESCE(FallBack, 1),
                    COALESCE(StreamOnly, 1)
                FROM SubActions_Backup
                WHERE SubActionTypes IN (1, 11)
                ORDER BY ActionTypeId, `Index`;

                -- Migrate Alert (SubActionTypes = 2)
                INSERT INTO SubActions_Alert (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId, Duration, Volume, CSS)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    COALESCE(Duration, 3),
                    COALESCE(Volume, 0.8),
                    COALESCE(CSS, '')
                FROM SubActions_Backup
                WHERE SubActionTypes = 2
                ORDER BY ActionTypeId, `Index`;

                -- Migrate PlaySound (SubActionTypes = 3)
                INSERT INTO SubActions_PlaySound (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId
                FROM SubActions_Backup
                WHERE SubActionTypes = 3
                ORDER BY ActionTypeId, `Index`;

                -- Migrate WriteFile (SubActionTypes = 4)
                INSERT INTO SubActions_WriteFile (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId, Append)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    COALESCE(Append, 1)
                FROM SubActions_Backup
                WHERE SubActionTypes = 4
                ORDER BY ActionTypeId, `Index`;

                -- Migrate RandomInt (SubActionTypes = 5)
                INSERT INTO SubActions_RandomInt (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId, Min, Max)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    COALESCE(Min, 0),
                    COALESCE(Max, 100)
                FROM SubActions_Backup
                WHERE SubActionTypes = 5
                ORDER BY ActionTypeId, `Index`;

                -- Migrate CurrentTime (SubActionTypes = 6)
                INSERT INTO SubActions_CurrentTime (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId
                FROM SubActions_Backup
                WHERE SubActionTypes = 6
                ORDER BY ActionTypeId, `Index`;

                -- Migrate FollowAge (SubActionTypes = 7)
                INSERT INTO SubActions_FollowAge (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId
                FROM SubActions_Backup
                WHERE SubActionTypes = 7
                ORDER BY ActionTypeId, `Index`;

                -- Migrate Uptime (SubActionTypes = 8)
                INSERT INTO SubActions_Uptime (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId
                FROM SubActions_Backup
                WHERE SubActionTypes = 8
                ORDER BY ActionTypeId, `Index`;

                -- Migrate ExternalApi (SubActionTypes = 9)
                INSERT INTO SubActions_ExternalApi (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId, HttpMethod, Headers)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId,
                    COALESCE(HttpMethod, 'GET'),
                    COALESCE(Headers, 'Accept: text/plain')
                FROM SubActions_Backup
                WHERE SubActionTypes = 9
                ORDER BY ActionTypeId, `Index`;

                -- Migrate WatchTime (SubActionTypes = 10)
                INSERT INTO SubActions_WatchTime (Id, `Index`, Text, `File`, Enabled, SubActionTypes, ActionTypeId)
                SELECT 
                    (@row_num := @row_num + 1) AS Id,
                    `Index`,
                    COALESCE(Text, ''),
                    COALESCE(`File`, ''),
                    Enabled,
                    SubActionTypes,
                    ActionTypeId
                FROM SubActions_Backup
                WHERE SubActionTypes = 10
                ORDER BY ActionTypeId, `Index`;

                -- Drop the temporary backup table
                DROP TEMPORARY TABLE IF EXISTS SubActions_Backup;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_WriteFile_ActionTypeId",
                table: "SubActions_WriteFile",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_Alert_ActionTypeId",
                table: "SubActions_Alert",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_CurrentTime_ActionTypeId",
                table: "SubActions_CurrentTime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_ExternalApi_ActionTypeId",
                table: "SubActions_ExternalApi",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_FollowAge_ActionTypeId",
                table: "SubActions_FollowAge",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_PlaySound_ActionTypeId",
                table: "SubActions_PlaySound",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_RandomInt_ActionTypeId",
                table: "SubActions_RandomInt",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_SendMessage_ActionTypeId",
                table: "SubActions_SendMessage",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_Uptime_ActionTypeId",
                table: "SubActions_Uptime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubActions_WatchTime_ActionTypeId",
                table: "SubActions_WatchTime",
                column: "ActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back this migration is not supported. This migration converts SubActions from TPH with GUID IDs to TPC with Int IDs, which cannot be safely reversed.");
        }
    }
}
