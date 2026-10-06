using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentEditAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentEditAudits",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEditAudits", x => x.Id);
                    table.CheckConstraint("CK_DocumentEditAudit_Version", "[Version] >= 2");
                    table.ForeignKey(
                        name: "FK_DocumentEditAudits_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEditAudits_DocumentId_Version",
                schema: "document",
                table: "DocumentEditAudits",
                columns: new[] { "DocumentId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentEditAudits",
                schema: "document");
        }
    }
}
