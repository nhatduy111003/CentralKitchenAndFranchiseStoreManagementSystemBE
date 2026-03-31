using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_ledger_entries",
                columns: table => new
                {
                    InventoryLedgerEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: true),
                    BatchCodeSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BatchCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAtSnapshot = table.Column<DateOnly>(type: "date", nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    StockBucket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeltaQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryPlanId = table.Column<int>(type: "integer", nullable: true),
                    StoreOrderId = table.Column<int>(type: "integer", nullable: true),
                    RequestedQuantitySnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ActualQuantitySnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DroppedQuantitySnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DropReasonSnapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CounterpartyScopeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CounterpartyScopeId = table.Column<int>(type: "integer", nullable: true),
                    CounterpartyBatchId = table.Column<int>(type: "integer", nullable: true),
                    IsNonStockEvent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_ledger_entries", x => x.InventoryLedgerEntryId);
                    table.ForeignKey(
                        name: "FK_inventory_ledger_entries_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "deliveries",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_ledger_entries_delivery_plans_DeliveryPlanId",
                        column: x => x.DeliveryPlanId,
                        principalTable: "delivery_plans",
                        principalColumn: "DeliveryPlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_ledger_entries_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalTable: "store_orders",
                        principalColumn: "StoreOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_ledger_entries_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_ActorUserId",
                table: "inventory_ledger_entries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_batch_occurred_id",
                table: "inventory_ledger_entries",
                columns: new[] { "BatchId", "OccurredAtUtc", "InventoryLedgerEntryId" },
                filter: "\"BatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_delivery_occurred_id",
                table: "inventory_ledger_entries",
                columns: new[] { "DeliveryId", "OccurredAtUtc", "InventoryLedgerEntryId" },
                filter: "\"DeliveryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_DeliveryPlanId",
                table: "inventory_ledger_entries",
                column: "DeliveryPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_item_occurred_id",
                table: "inventory_ledger_entries",
                columns: new[] { "ItemType", "ItemId", "OccurredAtUtc", "InventoryLedgerEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_scope_occurred_id",
                table: "inventory_ledger_entries",
                columns: new[] { "ScopeType", "ScopeId", "OccurredAtUtc", "InventoryLedgerEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ledger_entries_StoreOrderId",
                table: "inventory_ledger_entries",
                column: "StoreOrderId");

            migrationBuilder.CreateIndex(
                name: "UX_inventory_ledger_entries_correlation_sequence",
                table: "inventory_ledger_entries",
                columns: new[] { "CorrelationId", "SequenceNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_ledger_entries");
        }
    }
}
