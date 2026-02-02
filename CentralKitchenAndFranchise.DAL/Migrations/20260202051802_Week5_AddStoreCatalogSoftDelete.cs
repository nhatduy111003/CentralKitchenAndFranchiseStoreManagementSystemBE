using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Week5_AddStoreCatalogSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "store_catalog",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "store_catalog",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "store_catalog",
                type: "text",
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "store_catalog",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "store_catalog");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "store_catalog");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "store_catalog");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "store_catalog",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 0m);
        }
    }
}
