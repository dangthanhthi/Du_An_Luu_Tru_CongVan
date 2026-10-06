using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentRelations",
                schema: "document",
                columns: table => new
                {
                    IncomingDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutgoingDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRelations", x => new { x.IncomingDocumentId, x.OutgoingDocumentId });
                    table.CheckConstraint("CK_DocumentRelation_DifferentEnds", "[IncomingDocumentId] <> [OutgoingDocumentId]");
                    table.ForeignKey(
                        name: "FK_DocumentRelations_DocumentRegistrations_IncomingDocumentId",
                        column: x => x.IncomingDocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRelations_DocumentRegistrations_OutgoingDocumentId",
                        column: x => x.OutgoingDocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRelations_OutgoingDocumentId",
                schema: "document",
                table: "DocumentRelations",
                column: "OutgoingDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRelations",
                schema: "document");
        }
    }
}
