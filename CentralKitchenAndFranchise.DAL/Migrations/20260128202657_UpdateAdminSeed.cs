using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "password_hash",
                value: "$2a$11$LX2vA6.3yfqwNgiFTE4Gbu.WN8hT/YwH8mBIqwA4SjtWaDiD0D7PC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "password_hash",
                value: "$2a$11$u9YQkJz8q0Q0F9v6E2Hn2OqzX7QY8mJp8pXbZc9cYH9zZkPpHkR2a");
        }
    }
}
