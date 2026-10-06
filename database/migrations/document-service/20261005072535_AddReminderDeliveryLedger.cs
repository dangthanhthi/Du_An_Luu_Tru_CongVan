using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderDeliveryLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderFanoutManifest",
                schema: "document",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderFanoutManifest", x => x.BatchId);
                    table.ForeignKey(
                        name: "FK_ReminderFanoutManifest_ReminderBatch_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "document",
                        principalTable: "ReminderBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReminderDelivery",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InputterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
                    LeaseUntilUnix = table.Column<long>(type: "bigint", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextAttemptUnix = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotificationState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderDelivery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderDelivery_ReminderFanoutManifest_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "document",
                        principalTable: "ReminderFanoutManifest",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDelivery_BatchId_InputterUserId",
                schema: "document",
                table: "ReminderDelivery",
                columns: new[] { "BatchId", "InputterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDelivery_State_NextAttemptUnix",
                schema: "document",
                table: "ReminderDelivery",
                columns: new[] { "State", "NextAttemptUnix" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderDelivery",
                schema: "document");

            migrationBuilder.DropTable(
                name: "ReminderFanoutManifest",
                schema: "document");
        }
    }
}
