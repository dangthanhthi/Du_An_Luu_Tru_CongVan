using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessCatalogEntries",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Group = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCatalogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogAuditEvents",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistributionTargets",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Initial = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MappingState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionTargets", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "document",
                table: "BusinessCatalogEntries",
                columns: new[] { "Id", "Code", "Group", "IsActive", "Name", "SortOrder", "Version" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-4000-8000-000100000001"), "HL", "companies", true, "Hoàng Long", 1, 1L },
                    { new Guid("10000000-0000-4000-8000-000100000002"), "HV", "companies", true, "Hoàn Vũ", 2, 1L },
                    { new Guid("10000000-0000-4000-8000-000100000003"), "HLHV", "companies", true, "Hoàng Long Hoàn Vũ", 3, 1L },
                    { new Guid("10000000-0000-4000-8000-000200000001"), "FAX", "methods", true, "Fax", 1, 1L },
                    { new Guid("10000000-0000-4000-8000-000200000002"), "COURIER", "methods", true, "Courier", 2, 1L },
                    { new Guid("10000000-0000-4000-8000-000200000003"), "EMAIL", "methods", true, "eMail", 3, 1L },
                    { new Guid("10000000-0000-4000-8000-000200000004"), "HAND_DELIVER", "methods", true, "Pick-up/Hand-Deliver", 4, 1L },
                    { new Guid("10000000-0000-4000-8000-000200000005"), "EMAIL_FAX", "methods", true, "Email & Fax", 5, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000001"), "LETTER", "documentTypes", true, "Letter", 1, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000002"), "NOTIFICATION", "documentTypes", true, "Notification", 2, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000003"), "ANNOUNCEMENT", "documentTypes", true, "Announcement", 3, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000004"), "APPROVAL_REQUEST", "documentTypes", true, "Approval / Request", 4, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000005"), "INVITATION", "documentTypes", true, "Invitation", 5, 1L },
                    { new Guid("10000000-0000-4000-8000-000300000006"), "STATEMENT", "documentTypes", true, "Statement", 6, 1L },
                    { new Guid("10000000-0000-4000-8000-000400000001"), "MEMO", "internalTypes", true, "Inter-Office Memo", 1, 1L },
                    { new Guid("10000000-0000-4000-8000-000400000002"), "REPORT", "internalTypes", true, "Report", 2, 1L },
                    { new Guid("10000000-0000-4000-8000-000400000003"), "STATEMENT", "internalTypes", true, "Statement", 3, 1L },
                    { new Guid("10000000-0000-4000-8000-000400000004"), "PURCHASE_REQUEST", "internalTypes", true, "Purchase Request", 4, 1L },
                    { new Guid("10000000-0000-4000-8000-000400000005"), "OTHERS", "internalTypes", true, "Others", 5, 1L },
                    { new Guid("10000000-0000-4000-8000-000500000001"), "Normal", "sensitivity", true, "Normal", 1, 1L },
                    { new Guid("10000000-0000-4000-8000-000500000002"), "Confidential", "sensitivity", true, "Confidential", 2, 1L }
                });

            migrationBuilder.InsertData(
                schema: "document",
                table: "DistributionTargets",
                columns: new[] { "Id", "Initial", "IsActive", "LegacyId", "MappingState", "Name", "Version" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-4000-8000-000600000001"), "MGM", true, 1, "Pending", "Management", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000002"), "PRD", true, 2, "Pending", "Production", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000003"), "HSE", true, 3, "Pending", "HSE", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000004"), "PRJ", true, 4, "Pending", "Project", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000005"), "SUB", true, 5, "Pending", "Subsurface", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000006"), "FIN", true, 6, "Pending", "Finance", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000007"), "ADM", true, 7, "Pending", "Administration", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000008"), "C&P", true, 8, "Pending", "C&P", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000009"), "DRI", true, 9, "Pending", "Drilling", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000010"), null, true, 10, "Pending", "HLHV Partners", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000011"), null, true, 11, "Pending", "Secretary List", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000012"), null, true, 12, "Pending", "VT Shore Base", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000013"), null, true, 13, "Pending", "HLHV Members", 1L },
                    { new Guid("10000000-0000-4000-8000-000600000014"), "HLHVM", true, 14, "Pending", "HLHV Managers", 1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCatalogEntries_Group_Code",
                schema: "document",
                table: "BusinessCatalogEntries",
                columns: new[] { "Group", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogAuditEvents_EntryId",
                schema: "document",
                table: "CatalogAuditEvents",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionTargets_LegacyId",
                schema: "document",
                table: "DistributionTargets",
                column: "LegacyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessCatalogEntries",
                schema: "document");

            migrationBuilder.DropTable(
                name: "CatalogAuditEvents",
                schema: "document");

            migrationBuilder.DropTable(
                name: "DistributionTargets",
                schema: "document");
        }
    }
}
