using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <inheritdoc />
    public partial class ImproveAuthModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "auth",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "auth",
                table: "Roles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "auth",
                table: "Departments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "auth",
                table: "Departments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                schema: "auth",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "auth",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                schema: "auth",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'Admin')
                    INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'Admin', 'System administrator');
                IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'SecretaryDirector')
                    INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'SecretaryDirector', 'Director secretary');
                IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'SecretaryDept')
                    INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'SecretaryDept', 'Department secretary');
                IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'Staff')
                    INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'Staff', 'Department staff');
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                schema: "auth",
                table: "RefreshTokens",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                schema: "auth",
                table: "Users",
                column: "DepartmentId",
                principalSchema: "auth",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                schema: "auth",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DepartmentId",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                schema: "auth",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                schema: "auth",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "auth",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "auth",
                table: "Departments");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "auth",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
