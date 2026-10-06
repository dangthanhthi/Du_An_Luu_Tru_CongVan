using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentPdfReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentCurrentPdfs",
                schema: "document",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCurrentPdfs", x => x.DocumentId);
                    table.CheckConstraint("CK_CurrentPdf_Size", "[SizeBytes] BETWEEN 1 AND 26214400");
                    table.CheckConstraint("CK_CurrentPdf_State", "[State] IN ('Pending','Ready','Missing')");
                    table.CheckConstraint("CK_CurrentPdf_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_DocumentCurrentPdfs_DocumentRegistrations_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PdfReplacements",
                schema: "document",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    CommittedVersion = table.Column<long>(type: "bigint", nullable: true),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfReplacements", x => x.OperationId);
                    table.CheckConstraint("CK_PdfReplacement_State", "[State] IN ('Preparing','Committed','Aborted')");
                    table.CheckConstraint("CK_PdfReplacement_Version", "[ExpectedVersion] >= 1 AND ([CommittedVersion] IS NULL OR [CommittedVersion] > [ExpectedVersion])");
                    table.ForeignKey(
                        name: "FK_PdfReplacements_DocumentRegistrations_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "DocumentRegistrations",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCurrentPdfs_FileId",
                schema: "document",
                table: "DocumentCurrentPdfs",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCurrentPdfs_OperationId",
                schema: "document",
                table: "DocumentCurrentPdfs",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfReplacements_DocumentId",
                schema: "document",
                table: "PdfReplacements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfReplacements_FileId",
                schema: "document",
                table: "PdfReplacements",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfReplacements_State",
                schema: "document",
                table: "PdfReplacements",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCurrentPdfs",
                schema: "document");

            migrationBuilder.DropTable(
                name: "PdfReplacements",
                schema: "document");
        }
    }
}
