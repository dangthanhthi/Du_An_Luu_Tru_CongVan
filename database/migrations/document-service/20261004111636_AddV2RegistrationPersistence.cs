using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddV2RegistrationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentOutboxEvents",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutboxEvents", x => x.Id);
                    table.CheckConstraint("CK_DocumentOutbox_Attempts", "[Attempts] >= 0");
                    table.ForeignKey(
                        name: "FK_DocumentOutboxEvents_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRegistrations",
                schema: "document",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RegistrationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistrationYear = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CompanyNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerDepartmentCodeSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerDepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Sensitivity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRegistrations", x => x.DocumentId);
                    table.CheckConstraint("CK_Registration_Company", "[CompanyCode] IN ('HL', 'HV', 'HLHV')");
                    table.CheckConstraint("CK_Registration_Kind", "[Kind] IN ('INCOMING', 'OUTGOING', 'INTERNAL')");
                    table.CheckConstraint("CK_Registration_Sensitivity", "[Sensitivity] IN ('Normal', 'Confidential')");
                    table.CheckConstraint("CK_Registration_Sequence", "[SequenceNumber] BETWEEN 1 AND 99999");
                    table.CheckConstraint("CK_Registration_Version", "[Version] >= 1");
                    table.CheckConstraint("CK_Registration_Year", "[RegistrationYear] = YEAR([RegistrationDate])");
                    table.ForeignKey(
                        name: "FK_DocumentRegistrations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationRequests",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BodyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationRequests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "document",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutboxEvents_DocumentId_Type_AggregateVersion",
                schema: "document",
                table: "DocumentOutboxEvents",
                columns: new[] { "DocumentId", "Type", "AggregateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutboxEvents_State",
                schema: "document",
                table: "DocumentOutboxEvents",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrations_InputterUserId",
                schema: "document",
                table: "DocumentRegistrations",
                column: "InputterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrations_Kind_RegistrationYear_SequenceNumber",
                schema: "document",
                table: "DocumentRegistrations",
                columns: new[] { "Kind", "RegistrationYear", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrations_OriginatorUserId",
                schema: "document",
                table: "DocumentRegistrations",
                column: "OriginatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrations_OwnerDepartmentId_RegistrationDate",
                schema: "document",
                table: "DocumentRegistrations",
                columns: new[] { "OwnerDepartmentId", "RegistrationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRequests_ActorUserId_Kind_KeyHash",
                schema: "document",
                table: "RegistrationRequests",
                columns: new[] { "ActorUserId", "Kind", "KeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRequests_DocumentId",
                schema: "document",
                table: "RegistrationRequests",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOutboxEvents",
                schema: "document");

            migrationBuilder.DropTable(
                name: "DocumentRegistrations",
                schema: "document");

            migrationBuilder.DropTable(
                name: "RegistrationRequests",
                schema: "document");
        }
    }
}
