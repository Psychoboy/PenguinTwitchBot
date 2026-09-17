using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingModifySubAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subactions_fishingmodify",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CatchActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetFish = table.Column<string>(type: "TEXT", nullable: false),
                    TargetShopItem = table.Column<string>(type: "TEXT", nullable: false),
                    NewName = table.Column<string>(type: "TEXT", nullable: false),
                    NewGold = table.Column<string>(type: "TEXT", nullable: false),
                    NewCost = table.Column<string>(type: "TEXT", nullable: false),
                    NewDescription = table.Column<string>(type: "TEXT", nullable: false),
                    RarityMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ManualRarity = table.Column<int>(type: "INTEGER", nullable: true),
                    EnabledState = table.Column<int>(type: "INTEGER", nullable: false),
                    NewBoostType = table.Column<int>(type: "INTEGER", nullable: true),
                    NewBoostAmount = table.Column<string>(type: "TEXT", nullable: false),
                    NewTargetFish = table.Column<string>(type: "TEXT", nullable: false),
                    NewTargetCategory = table.Column<string>(type: "TEXT", nullable: false),
                    NewEquipmentSlot = table.Column<string>(type: "TEXT", nullable: false),
                    NewMaxUses = table.Column<string>(type: "TEXT", nullable: false),
                    AdminOnlyState = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishingmodify", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishingmodify_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subactions_fishingmodify_Actions_CatchActionTypeId",
                        column: x => x.CatchActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingmodify_ActionTypeId",
                table: "subactions_fishingmodify",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishingmodify_CatchActionTypeId",
                table: "subactions_fishingmodify",
                column: "CatchActionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subactions_fishingmodify");
        }
    }
}
