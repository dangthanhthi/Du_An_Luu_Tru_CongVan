using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartnerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalEntityContactsAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Partners_ShortName",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_Partners_TaxCode",
                schema: "partner",
                table: "Partners");

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ContactInformation",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedShortName",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTaxCode",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "partner",
                table: "Partners",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "PartnerAudits",
                schema: "partner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerAudits_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "partner",
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve displayed legacy values, but reserve their canonical keys before
            // adding the new indexes. Conflicting trimmed legacy keys abort migration;
            // they must be reviewed rather than silently renamed or deleted.
            migrationBuilder.Sql("""
                UPDATE [partner].[Partners] SET
                  [NormalizedShortName] = NULLIF(UPPER(LTRIM(RTRIM([ShortName]))), N''),
                  [NormalizedTaxCode] = NULLIF(UPPER(LTRIM(RTRIM([TaxCode]))), N'');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_NormalizedShortName",
                schema: "partner",
                table: "Partners",
                column: "NormalizedShortName",
                unique: true,
                filter: "[NormalizedShortName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_NormalizedTaxCode",
                schema: "partner",
                table: "Partners",
                column: "NormalizedTaxCode",
                unique: true,
                filter: "[NormalizedTaxCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAudits_PartnerId_Version",
                schema: "partner",
                table: "PartnerAudits",
                columns: new[] { "PartnerId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerAudits",
                schema: "partner");

            migrationBuilder.DropIndex(
                name: "IX_Partners_NormalizedShortName",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_Partners_NormalizedTaxCode",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "ContactInformation",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "NormalizedShortName",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "NormalizedTaxCode",
                schema: "partner",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "partner",
                table: "Partners");

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                schema: "partner",
                table: "Partners",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_ShortName",
                schema: "partner",
                table: "Partners",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TaxCode",
                schema: "partner",
                table: "Partners",
                column: "TaxCode",
                unique: true,
                filter: "[TaxCode] IS NOT NULL");
        }
    }
}
