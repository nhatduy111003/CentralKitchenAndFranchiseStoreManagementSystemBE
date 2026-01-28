using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "email", "password_hash" },
                values: new object[] { "admin@gmail.com", "$2a$11$u9YQkJz8q0Q0F9v6E2Hn2OqzX7QY8mJp8pXbZc9cYH9zZkPpHkR2a" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "email", "password_hash" },
                values: new object[] { "admin@local", "$2a$11$REPLACE_WITH_BCRYPT_HASH" });
        }
    }
}
