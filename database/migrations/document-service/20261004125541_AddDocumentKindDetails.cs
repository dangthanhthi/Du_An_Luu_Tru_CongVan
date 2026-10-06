using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentKindDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentKindDetails",
                schema: "document",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SenderPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MethodCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MethodNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DocumentTypeNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CategoryNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OtherRecipients = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Others = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentKindDetails", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentKindDetails_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRecipients",
                schema: "document",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRecipients", x => new { x.DocumentId, x.ReferenceType, x.ReferenceId });
                    table.CheckConstraint("CK_DocumentRecipient_Type", "[ReferenceType] IN ('ExternalEntity', 'DistributionTarget')");
                    table.ForeignKey(
                        name: "FK_DocumentRecipients_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentKindDetails_SenderPartnerId",
                schema: "document",
                table: "DocumentKindDetails",
                column: "SenderPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRecipients_ReferenceType_ReferenceId",
                schema: "document",
                table: "DocumentRecipients",
                columns: new[] { "ReferenceType", "ReferenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentKindDetails",
                schema: "document");

            migrationBuilder.DropTable(
                name: "DocumentRecipients",
                schema: "document");
        }
    }
}
