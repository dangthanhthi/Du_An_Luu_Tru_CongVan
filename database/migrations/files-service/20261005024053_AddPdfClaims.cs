using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfClaims",
                schema: "files",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfClaims", x => x.OperationId);
                    table.CheckConstraint("CK_PdfClaim_State", "[State] IN ('Prepared','Active','Retired','Deleted')");
                    table.CheckConstraint("CK_PdfClaim_Version", "[Version] >= 1 AND [ExpectedVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_PdfClaims_PdfUploads_FileId",
                        column: x => x.FileId,
                        principalSchema: "files",
                        principalTable: "PdfUploads",
                        principalColumn: "FileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfClaims_FileId",
                schema: "files",
                table: "PdfClaims",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfClaims_State",
                schema: "files",
                table: "PdfClaims",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfClaims",
                schema: "files");
        }
    }
}
