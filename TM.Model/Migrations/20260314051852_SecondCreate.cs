using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM.Model.Migrations
{
    /// <inheritdoc />
    public partial class SecondCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsDeleted", "PasswordHash", "Role", "Username" },
                values: new object[] { 99, new DateTime(2026, 3, 14, 5, 18, 51, 140, DateTimeKind.Utc).AddTicks(1595), "superadmin@jira.com", false, "$2a$11$KkvZ2xreI5YUC4FMAtkbk./7T4Mrr0AjLQGX24nEEn9/4W4XqgGfO", "SuperAdmin", "superadmin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 99);
        }
    }
}
