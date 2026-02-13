using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Week5_Task18_RbacHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "roles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "permissions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "GroupName",
                table: "permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "permissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "permissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "permissions");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "permissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "GroupName",
                table: "permissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "permissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
