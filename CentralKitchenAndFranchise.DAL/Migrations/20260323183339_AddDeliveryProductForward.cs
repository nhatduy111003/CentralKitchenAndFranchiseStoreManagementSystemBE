using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryProductForward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_work_assignments_owner",
                table: "user_work_assignments");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "delivery_product_items",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "DropReason",
                table: "delivery_product_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDropped",
                table: "delivery_product_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedQuantity",
                table: "delivery_product_items",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_work_assignments_owner",
                table: "user_work_assignments",
                sql: "\r\n                (\r\n                    (\"AssignmentType\" = 'FRANCHISE' AND \"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL)\r\n                    OR\r\n                    (\"AssignmentType\" = 'CENTRAL_KITCHEN' AND \"FranchiseId\" IS NULL AND \"CentralKitchenId\" IS NOT NULL)\r\n                )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_work_assignments_owner",
                table: "user_work_assignments");

            migrationBuilder.DropColumn(
                name: "DropReason",
                table: "delivery_product_items");

            migrationBuilder.DropColumn(
                name: "IsDropped",
                table: "delivery_product_items");

            migrationBuilder.DropColumn(
                name: "RequestedQuantity",
                table: "delivery_product_items");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "delivery_product_items",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_work_assignments_owner",
                table: "user_work_assignments",
                sql: "\r\n    (\r\n        (\"AssignmentType\" = 'FRANCHISE' AND \"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL)\r\n        OR\r\n        (\"AssignmentType\" = 'CENTRAL_KITCHEN' AND \"FranchiseId\" IS NULL AND \"CentralKitchenId\" IS NOT NULL)\r\n    )");
        }
    }
}
