using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryInbox",
                schema: "auth",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryInbox", x => new { x.SourceId, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "DirectoryOutbox",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AuthorizationRevision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryProjections",
                schema: "auth",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationRevision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryProjections", x => x.SourceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryOutbox_PublishedAt",
                schema: "auth",
                table: "DirectoryOutbox",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryOutbox_SourceId_AuthorizationRevision",
                schema: "auth",
                table: "DirectoryOutbox",
                columns: new[] { "SourceId", "AuthorizationRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryInbox",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "DirectoryOutbox",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "DirectoryProjections",
                schema: "auth");
        }
    }
}
