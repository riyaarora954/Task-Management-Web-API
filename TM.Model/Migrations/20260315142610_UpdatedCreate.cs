using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM.Model.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsDeleted", "PasswordHash", "Role", "Username" },
                values: new object[] { 1, new DateTime(2026, 3, 15, 14, 26, 9, 577, DateTimeKind.Utc).AddTicks(6113), "superadmin@jira.com", false, "$2a$11$S3qWRwt8VTW/PNnkZj4Wjul2iVo83Z7QNQwTZ7HxY6UbVfZRRg3Vm", "SuperAdmin", "superadmin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsDeleted", "PasswordHash", "Role", "Username" },
                values: new object[] { 1, new DateTime(2026, 3, 14, 5, 18, 51, 140, DateTimeKind.Utc).AddTicks(1595), "superadmin@jira.com", false, "$2a$11$KkvZ2xreI5YUC4FMAtkbk./7T4Mrr0AjLQGX24nEEn9/4W4XqgGfO", "SuperAdmin", "superadmin" });
        }
    }
}
