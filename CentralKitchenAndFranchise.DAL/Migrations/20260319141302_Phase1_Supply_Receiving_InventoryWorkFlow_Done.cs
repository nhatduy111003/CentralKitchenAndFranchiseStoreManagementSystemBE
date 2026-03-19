using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Supply_Receiving_InventoryWorkFlow_Done : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_receiving_reports_users_ReceivedByUserId",
                table: "receiving_reports");

            migrationBuilder.DropForeignKey(
                name: "FK_store_order_histories_users_PerformedByUserId",
                table: "store_order_histories");

            migrationBuilder.DropIndex(
                name: "IX_store_order_histories_PerformedByUserId",
                table: "store_order_histories");

            migrationBuilder.DropIndex(
                name: "IX_store_order_histories_StoreOrderId_PerformedAt",
                table: "store_order_histories");

            migrationBuilder.DropIndex(
                name: "IX_receiving_reports_ReceivedByUserId",
                table: "receiving_reports");

            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "store_order_histories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewStatus",
                table: "store_order_histories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "receiving_reports",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveredAt",
                table: "deliveries",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_histories_StoreOrderId",
                table: "store_order_histories",
                column: "StoreOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_store_order_histories_StoreOrderId",
                table: "store_order_histories");

            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "store_order_histories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewStatus",
                table: "store_order_histories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "receiving_reports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveredAt",
                table: "deliveries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_order_histories_PerformedByUserId",
                table: "store_order_histories",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_histories_StoreOrderId_PerformedAt",
                table: "store_order_histories",
                columns: new[] { "StoreOrderId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_ReceivedByUserId",
                table: "receiving_reports",
                column: "ReceivedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_receiving_reports_users_ReceivedByUserId",
                table: "receiving_reports",
                column: "ReceivedByUserId",
                principalTable: "users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_store_order_histories_users_PerformedByUserId",
                table: "store_order_histories",
                column: "PerformedByUserId",
                principalTable: "users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
