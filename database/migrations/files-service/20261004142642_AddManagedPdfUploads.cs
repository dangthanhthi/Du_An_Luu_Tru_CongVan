using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilesService.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedPdfUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfUploads",
                schema: "files",
                columns: table => new
                {
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfUploads", x => x.FileId);
                    table.CheckConstraint("CK_PdfUpload_Size", "[SizeBytes] IS NULL OR [SizeBytes] BETWEEN 1 AND 26214400");
                    table.CheckConstraint("CK_PdfUpload_State", "[State] IN ('Receiving','Available','PendingScan','Rejected','Failed','Missing')");
                    table.CheckConstraint("CK_PdfUpload_Version", "[Version] >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfUploads_State_CreatedAt",
                schema: "files",
                table: "PdfUploads",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfUploads_StorageKey",
                schema: "files",
                table: "PdfUploads",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfUploads_UploaderUserId",
                schema: "files",
                table: "PdfUploads",
                column: "UploaderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfUploads",
                schema: "files");
        }
    }
}
