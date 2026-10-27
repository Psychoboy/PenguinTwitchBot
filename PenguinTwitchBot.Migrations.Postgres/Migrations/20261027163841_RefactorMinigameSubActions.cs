using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMinigameSubActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegCount",
                table: "subactions_starship_start_expedition",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaxEventIntervalMinutes",
                table: "subactions_starship_start_expedition",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MinEventIntervalMinutes",
                table: "subactions_starship_start_expedition",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Seed",
                table: "subactions_starship_start_expedition",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "subactions_balloon_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_balloon_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_balloon_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_balloon_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_balloon_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_curling_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_curling_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_curling_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_curling_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_curling_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_curling_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_curling_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_curling_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_golf_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_golf_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_golf_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_golf_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_golf_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_golf_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_golf_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_golf_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_parachute_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_parachute_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_parachute_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_parachute_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_parachute_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_parachute_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_parachute_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_parachute_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_plinko_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_plinko_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_plinko_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_plinko_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_plinko_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_plinko_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_plinko_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_plinko_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_racing_join",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EntryMode = table.Column<string>(type: "text", nullable: false),
                    EmoteOrImage = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_racing_join", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_racing_join_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_racing_join_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_racing_start_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    GameMode = table.Column<string>(type: "text", nullable: false),
                    TrackType = table.Column<string>(type: "text", nullable: false),
                    TotalLaps = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<string>(type: "text", nullable: false),
                    ThemeOverrideId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_racing_start_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_racing_start_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_racing_start_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_racing_stop_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    ClearQueue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_racing_stop_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_racing_stop_session_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_racing_stop_session_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_start_session_ActionTypeId",
                table: "subactions_balloon_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_start_session_CatchActionTypeId",
                table: "subactions_balloon_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_stop_session_ActionTypeId",
                table: "subactions_balloon_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_balloon_stop_session_CatchActionTypeId",
                table: "subactions_balloon_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_curling_start_session_ActionTypeId",
                table: "subactions_curling_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_curling_start_session_CatchActionTypeId",
                table: "subactions_curling_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_curling_stop_session_ActionTypeId",
                table: "subactions_curling_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_curling_stop_session_CatchActionTypeId",
                table: "subactions_curling_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_golf_start_session_ActionTypeId",
                table: "subactions_golf_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_golf_start_session_CatchActionTypeId",
                table: "subactions_golf_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_golf_stop_session_ActionTypeId",
                table: "subactions_golf_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_golf_stop_session_CatchActionTypeId",
                table: "subactions_golf_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_parachute_start_session_ActionTypeId",
                table: "subactions_parachute_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_parachute_start_session_CatchActionTypeId",
                table: "subactions_parachute_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_parachute_stop_session_ActionTypeId",
                table: "subactions_parachute_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_parachute_stop_session_CatchActionTypeId",
                table: "subactions_parachute_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_plinko_start_session_ActionTypeId",
                table: "subactions_plinko_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_plinko_start_session_CatchActionTypeId",
                table: "subactions_plinko_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_plinko_stop_session_ActionTypeId",
                table: "subactions_plinko_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_plinko_stop_session_CatchActionTypeId",
                table: "subactions_plinko_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_join_ActionTypeId",
                table: "subactions_racing_join",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_join_CatchActionTypeId",
                table: "subactions_racing_join",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_start_session_ActionTypeId",
                table: "subactions_racing_start_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_start_session_CatchActionTypeId",
                table: "subactions_racing_start_session",
                column: "CatchActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_stop_session_ActionTypeId",
                table: "subactions_racing_stop_session",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_racing_stop_session_CatchActionTypeId",
                table: "subactions_racing_stop_session",
                column: "CatchActionTypeId");

            migrationBuilder.Sql(@"
                INSERT INTO ""subactions_plinko_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 85, ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") = 'plinko';

                INSERT INTO ""subactions_balloon_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 87, ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") = 'balloon';

                INSERT INTO ""subactions_curling_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 89, ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") = 'curling';

                INSERT INTO ""subactions_golf_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 91, ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") = 'golf';

                INSERT INTO ""subactions_racing_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameMode"", ""TrackType"", ""TotalLaps"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 93, ""ActionTypeId"", ""CatchActionTypeId"", '', '', '', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") = 'racing';

                INSERT INTO ""subactions_parachute_start_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 83, ""ActionTypeId"", ""CatchActionTypeId"", ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_start_drop_game_session""
                WHERE LOWER(""GameType"") NOT IN ('plinko', 'balloon', 'curling', 'golf', 'racing');

                DELETE FROM ""subactions_start_drop_game_session"";

                INSERT INTO ""subactions_parachute_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 84, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'parachute';

                INSERT INTO ""subactions_plinko_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 86, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'plinko';

                INSERT INTO ""subactions_balloon_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 88, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'balloon';

                INSERT INTO ""subactions_curling_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 90, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'curling';

                INSERT INTO ""subactions_golf_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 92, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'golf';

                INSERT INTO ""subactions_racing_stop_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""ClearQueue"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 94, ""ActionTypeId"", ""CatchActionTypeId"", TRUE
                FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") = 'racing';

                DELETE FROM ""subactions_stop_drop_game_session""
                WHERE LOWER(""GameType"") IN ('parachute', 'plinko', 'balloon', 'curling', 'golf', 'racing');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Parachute', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_parachute_start_session"";

                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Plinko', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_plinko_start_session"";

                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Balloon', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_balloon_start_session"";

                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Curling', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_curling_start_session"";

                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Golf', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_golf_start_session"";

                INSERT INTO ""subactions_start_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"", ""DurationSeconds"", ""ThemeOverrideId"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 70, ""ActionTypeId"", ""CatchActionTypeId"", 'Racing', ""DurationSeconds"", ""ThemeOverrideId""
                FROM ""subactions_racing_start_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Parachute'
                FROM ""subactions_parachute_stop_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Plinko'
                FROM ""subactions_plinko_stop_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Balloon'
                FROM ""subactions_balloon_stop_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Curling'
                FROM ""subactions_curling_stop_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Golf'
                FROM ""subactions_golf_stop_session"";

                INSERT INTO ""subactions_stop_drop_game_session"" (""Id"", ""Index"", ""Text"", ""Enabled"", ""SubActionTypes"", ""ActionTypeId"", ""CatchActionTypeId"", ""GameType"")
                SELECT ""Id"", ""Index"", ""Text"", ""Enabled"", 71, ""ActionTypeId"", ""CatchActionTypeId"", 'Racing'
                FROM ""subactions_racing_stop_session"";
            ");

            migrationBuilder.DropTable(
                name: "subactions_balloon_start_session");

            migrationBuilder.DropTable(
                name: "subactions_balloon_stop_session");

            migrationBuilder.DropTable(
                name: "subactions_curling_start_session");

            migrationBuilder.DropTable(
                name: "subactions_curling_stop_session");

            migrationBuilder.DropTable(
                name: "subactions_golf_start_session");

            migrationBuilder.DropTable(
                name: "subactions_golf_stop_session");

            migrationBuilder.DropTable(
                name: "subactions_parachute_start_session");

            migrationBuilder.DropTable(
                name: "subactions_parachute_stop_session");

            migrationBuilder.DropTable(
                name: "subactions_plinko_start_session");

            migrationBuilder.DropTable(
                name: "subactions_plinko_stop_session");

            migrationBuilder.DropTable(
                name: "subactions_racing_join");

            migrationBuilder.DropTable(
                name: "subactions_racing_start_session");

            migrationBuilder.DropTable(
                name: "subactions_racing_stop_session");

            migrationBuilder.DropColumn(
                name: "LegCount",
                table: "subactions_starship_start_expedition");

            migrationBuilder.DropColumn(
                name: "MaxEventIntervalMinutes",
                table: "subactions_starship_start_expedition");

            migrationBuilder.DropColumn(
                name: "MinEventIntervalMinutes",
                table: "subactions_starship_start_expedition");

            migrationBuilder.DropColumn(
                name: "Seed",
                table: "subactions_starship_start_expedition");
        }
    }
}
