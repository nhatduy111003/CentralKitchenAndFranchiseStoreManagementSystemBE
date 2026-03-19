using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_OrderWorkflow_Receiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_store_orders_franchises_FranchiseId",
                table: "store_orders");

            migrationBuilder.DropIndex(
                name: "IX_store_orders_FranchiseId",
                table: "store_orders");

            migrationBuilder.DropIndex(
                name: "IX_receiving_reports_DeliveryId",
                table: "receiving_reports");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "store_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CancelReason",
                table: "store_orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatusNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryStatusUpdatedAt",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatusUpdatedByUserId",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForwardNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ForwardedAt",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForwardedByUserId",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparedByUserId",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparingNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingNoteUpdatedAt",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingNoteUpdatedByUserId",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiveNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedByUserId",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreNote",
                table: "store_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "receiving_reports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedByUserId",
                table: "receiving_reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreOrderId",
                table: "delivery_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "store_order_histories",
                columns: table => new
                {
                    StoreOrderHistoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StoreOrderId = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActionLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_histories", x => x.StoreOrderHistoryId);
                    table.ForeignKey(
                        name: "FK_store_order_histories_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalTable: "store_orders",
                        principalColumn: "StoreOrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_order_histories_users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_FranchiseId_OrderDate",
                table: "store_orders",
                columns: new[] { "FranchiseId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_Status",
                table: "store_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_DeliveryId",
                table: "receiving_reports",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_ReceivedByUserId",
                table: "receiving_reports",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_delivery_plans_store_order_id",
                table: "delivery_plans",
                column: "StoreOrderId",
                unique: true,
                filter: "\"StoreOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_histories_PerformedByUserId",
                table: "store_order_histories",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_histories_StoreOrderId_PerformedAt",
                table: "store_order_histories",
                columns: new[] { "StoreOrderId", "PerformedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_plans_store_orders_StoreOrderId",
                table: "delivery_plans",
                column: "StoreOrderId",
                principalTable: "store_orders",
                principalColumn: "StoreOrderId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_receiving_reports_users_ReceivedByUserId",
                table: "receiving_reports",
                column: "ReceivedByUserId",
                principalTable: "users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_store_orders_franchises_FranchiseId",
                table: "store_orders",
                column: "FranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_plans_store_orders_StoreOrderId",
                table: "delivery_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_receiving_reports_users_ReceivedByUserId",
                table: "receiving_reports");

            migrationBuilder.DropForeignKey(
                name: "FK_store_orders_franchises_FranchiseId",
                table: "store_orders");

            migrationBuilder.DropTable(
                name: "store_order_histories");

            migrationBuilder.DropIndex(
                name: "IX_store_orders_FranchiseId_OrderDate",
                table: "store_orders");

            migrationBuilder.DropIndex(
                name: "IX_store_orders_Status",
                table: "store_orders");

            migrationBuilder.DropIndex(
                name: "IX_receiving_reports_DeliveryId",
                table: "receiving_reports");

            migrationBuilder.DropIndex(
                name: "IX_receiving_reports_ReceivedByUserId",
                table: "receiving_reports");

            migrationBuilder.DropIndex(
                name: "UX_delivery_plans_store_order_id",
                table: "delivery_plans");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusUpdatedAt",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusUpdatedByUserId",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ForwardNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ForwardedAt",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ForwardedByUserId",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "PreparedByUserId",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "PreparingNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ProcessingNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ProcessingNoteUpdatedAt",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ProcessingNoteUpdatedByUserId",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ReceiveNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "ReceivedByUserId",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "StoreNote",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "ReceivedByUserId",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "StoreOrderId",
                table: "delivery_plans");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "store_orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "CancelReason",
                table: "store_orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_FranchiseId",
                table: "store_orders",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_DeliveryId",
                table: "receiving_reports",
                column: "DeliveryId");

            migrationBuilder.AddForeignKey(
                name: "FK_store_orders_franchises_FranchiseId",
                table: "store_orders",
                column: "FranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
