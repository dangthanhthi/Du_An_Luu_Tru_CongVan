using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailWorkerService.Migrations
{
    /// <inheritdoc />
    public partial class EmailWorkerBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "emailworker");

            migrationBuilder.CreateTable(
                name: "EmailImapSettings",
                schema: "emailworker",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImapHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImapPort = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WhitelistedDomains = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AutoScanIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailImapSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailScanLogs",
                schema: "emailworker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailsScanned = table.Column<int>(type: "int", nullable: false),
                    TotalEmails = table.Column<int>(type: "int", nullable: false),
                    DocumentsCreated = table.Column<int>(type: "int", nullable: false),
                    ReadyForIntakeCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    CurrentEmailSubject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrentSenderEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TriggerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailScanLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailScanItemLogs",
                schema: "emailworker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttachmentName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtractedReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExtractedSubject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntakeConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailScanItemLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailScanItemLogs_EmailScanLogs_ScanLogId",
                        column: x => x.ScanLogId,
                        principalSchema: "emailworker",
                        principalTable: "EmailScanLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailScanItemLogs_ScanLogId",
                schema: "emailworker",
                table: "EmailScanItemLogs",
                column: "ScanLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailImapSettings",
                schema: "emailworker");

            migrationBuilder.DropTable(
                name: "EmailScanItemLogs",
                schema: "emailworker");

            migrationBuilder.DropTable(
                name: "EmailScanLogs",
                schema: "emailworker");
        }
    }
}
