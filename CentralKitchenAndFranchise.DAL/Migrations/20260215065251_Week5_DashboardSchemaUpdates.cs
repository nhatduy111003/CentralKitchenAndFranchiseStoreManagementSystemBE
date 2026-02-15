using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Week5_DashboardSchemaUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "production_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DRAFT");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "franchises",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "franchises",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "production_plans");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "franchises");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "franchises");
        }
    }
}
