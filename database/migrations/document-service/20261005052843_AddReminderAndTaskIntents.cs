using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderAndTaskIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTaskIntent",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BodyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RemoteTaskId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseUntilUnix = table.Column<long>(type: "bigint", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTaskIntent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTaskIntent_DocumentRegistrations_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReminderBatch",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntilUnix = table.Column<long>(type: "bigint", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderBatch", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTaskIntent_ActorId_KeyHash",
                schema: "document",
                table: "DocumentTaskIntent",
                columns: new[] { "ActorId", "KeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTaskIntent_DocumentId",
                schema: "document",
                table: "DocumentTaskIntent",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderBatch_DepartmentId_Period",
                schema: "document",
                table: "ReminderBatch",
                columns: new[] { "DepartmentId", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentTaskIntent",
                schema: "document");

            migrationBuilder.DropTable(
                name: "ReminderBatch",
                schema: "document");
        }
    }
}
