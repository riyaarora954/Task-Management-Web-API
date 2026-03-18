using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TM.Model.Migrations
{
    /// <inheritdoc />
    public partial class DbCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 17, 13, 11, 48, 906, DateTimeKind.Utc).AddTicks(819), "$2a$11$S8s2ByBRaDRnhJmFxeCAW.8ZegykS7.Y36O5C3AcbcUq1.xqn7WRe" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 15, 14, 26, 9, 577, DateTimeKind.Utc).AddTicks(6113), "$2a$11$S3qWRwt8VTW/PNnkZj4Wjul2iVo83Z7QNQwTZ7HxY6UbVfZRRg3Vm" });
        }
    }
}
