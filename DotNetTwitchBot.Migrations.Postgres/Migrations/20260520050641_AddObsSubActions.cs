using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddObsSubActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_obs_setbrowsersourceurl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setbrowsersourceurl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setbrowsersourceurl_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setcolorsourcecolor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setcolorsourcecolor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setcolorsourcecolor_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setimagesourcefile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setimagesourcefile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setimagesourcefile_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setinputmute",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    Muted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setinputmute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setinputmute_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setmediasourcefile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setmediasourcefile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setmediasourcefile_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setmediastate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    MediaAction = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setmediastate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setmediastate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setsourceaudiotrackstate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    TrackNumber = table.Column<int>(type: "integer", nullable: false),
                    TrackEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setsourceaudiotrackstate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setsourceaudiotrackstate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setsourcefilterstate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(type: "TEXT", nullable: false),
                    FilterName = table.Column<string>(type: "TEXT", nullable: false),
                    FilterEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setsourcefilterstate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setsourcefilterstate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setsourcevisibility",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    SceneName = table.Column<string>(type: "TEXT", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setsourcevisibility", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setsourcevisibility_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_settext",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubActionTypes = table.Column<int>(type: "integer", nullable: false),
                    ActionTypeId = table.Column<int>(type: "integer", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "integer", nullable: true),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    TextContent = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_settext", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_settext_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setbrowsersourceurl_ActionTypeId",
                table: "subactions_obs_setbrowsersourceurl",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setcolorsourcecolor_ActionTypeId",
                table: "subactions_obs_setcolorsourcecolor",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setimagesourcefile_ActionTypeId",
                table: "subactions_obs_setimagesourcefile",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setinputmute_ActionTypeId",
                table: "subactions_obs_setinputmute",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setmediasourcefile_ActionTypeId",
                table: "subactions_obs_setmediasourcefile",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setmediastate_ActionTypeId",
                table: "subactions_obs_setmediastate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourceaudiotrackstate_ActionTypeId",
                table: "subactions_obs_setsourceaudiotrackstate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourcefilterstate_ActionTypeId",
                table: "subactions_obs_setsourcefilterstate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setsourcevisibility_ActionTypeId",
                table: "subactions_obs_setsourcevisibility",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_settext_ActionTypeId",
                table: "subactions_obs_settext",
                column: "ActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_obs_setbrowsersourceurl");

            migrationBuilder.DropTable(
                name: "subactions_obs_setcolorsourcecolor");

            migrationBuilder.DropTable(
                name: "subactions_obs_setimagesourcefile");

            migrationBuilder.DropTable(
                name: "subactions_obs_setinputmute");

            migrationBuilder.DropTable(
                name: "subactions_obs_setmediasourcefile");

            migrationBuilder.DropTable(
                name: "subactions_obs_setmediastate");

            migrationBuilder.DropTable(
                name: "subactions_obs_setsourceaudiotrackstate");

            migrationBuilder.DropTable(
                name: "subactions_obs_setsourcefilterstate");

            migrationBuilder.DropTable(
                name: "subactions_obs_setsourcevisibility");

            migrationBuilder.DropTable(
                name: "subactions_obs_settext");
        }
    }
}
