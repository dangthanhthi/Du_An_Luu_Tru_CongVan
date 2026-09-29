using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Migrations;

public partial class AddIncomingSourceMessageId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceMessageId",
            schema: "document",
            table: "Documents",
            type: "nvarchar(450)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Documents_SourceMessageId",
            schema: "document",
            table: "Documents",
            column: "SourceMessageId",
            unique: true,
            filter: "[SourceMessageId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Documents_SourceMessageId",
            schema: "document",
            table: "Documents");

        migrationBuilder.DropColumn(
            name: "SourceMessageId",
            schema: "document",
            table: "Documents");
    }
}
