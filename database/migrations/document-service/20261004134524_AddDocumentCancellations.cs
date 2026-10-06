using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentCancellations",
                schema: "document",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RestoredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RestoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCancellations", x => x.DocumentId);
                    table.CheckConstraint("CK_DocumentCancellation_PreviousStatus", "[PreviousStatus] IN ('InProgress', 'Distributed')");
                    table.CheckConstraint("CK_DocumentCancellation_Reason", "LEN(TRIM([Reason])) > 0");
                    table.CheckConstraint("CK_DocumentCancellation_Restore", "([RestoredAt] IS NULL AND [RestoredByUserId] IS NULL) OR ([RestoredAt] IS NOT NULL AND [RestoredByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DocumentCancellations_DocumentRegistrations_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCancellations",
                schema: "document");
        }
    }
}
