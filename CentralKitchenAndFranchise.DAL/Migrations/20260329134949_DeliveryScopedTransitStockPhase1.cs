using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryScopedTransitStockPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_central_kitchens_CentralKitchenId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_franchises_FranchiseId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_products_ProductId",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "UX_product_batches_product_ck_batchcode",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "UX_product_batches_product_franchise_batchcode",
                table: "product_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_batches_owner",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_ck_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_franchise_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryId",
                table: "product_batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInTransit",
                table: "product_batches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryId",
                table: "ingredient_batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInTransit",
                table: "ingredient_batches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockCommitted",
                table: "deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "deliveries",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_DeliveryId",
                table: "product_batches",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "UX_product_batches_product_ck_batchcode",
                table: "product_batches",
                columns: new[] { "ProductId", "CentralKitchenId", "BatchCode" },
                unique: true,
                filter: "\"CentralKitchenId\" IS NOT NULL AND \"FranchiseId\" IS NULL AND \"DeliveryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_product_batches_product_franchise_batchcode",
                table: "product_batches",
                columns: new[] { "ProductId", "FranchiseId", "BatchCode" },
                unique: true,
                filter: "\"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL AND \"DeliveryId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_batches_onhand_no_delivery",
                table: "product_batches",
                sql: "\"IsInTransit\" = true OR \"DeliveryId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_batches_owner",
                table: "product_batches",
                sql: "\r\n                        (\r\n                            \"FranchiseId\" IS NOT NULL\r\n                            AND \"CentralKitchenId\" IS NULL\r\n                        )\r\n                        OR\r\n                        (\r\n                            \"FranchiseId\" IS NULL\r\n                            AND \"CentralKitchenId\" IS NOT NULL\r\n                        )");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_batches_transit_has_delivery",
                table: "product_batches",
                sql: "\"IsInTransit\" = false OR \"DeliveryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_DeliveryId",
                table: "ingredient_batches",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "UX_ingredient_batches_ingredient_ck_batchcode",
                table: "ingredient_batches",
                columns: new[] { "IngredientId", "CentralKitchenId", "BatchCode" },
                unique: true,
                filter: "\"CentralKitchenId\" IS NOT NULL AND \"FranchiseId\" IS NULL AND \"DeliveryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ingredient_batches_ingredient_franchise_batchcode",
                table: "ingredient_batches",
                columns: new[] { "IngredientId", "FranchiseId", "BatchCode" },
                unique: true,
                filter: "\"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL AND \"DeliveryId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingredient_batches_onhand_no_delivery",
                table: "ingredient_batches",
                sql: "\"IsInTransit\" = true OR \"DeliveryId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingredient_batches_transit_has_delivery",
                table: "ingredient_batches",
                sql: "\"IsInTransit\" = false OR \"DeliveryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches",
                sql: "\r\n                        (\r\n                            \"Type\" = 'FRANCHISE'\r\n                            AND \"FranchiseId\" IS NOT NULL\r\n                            AND \"CentralKitchenId\" IS NULL\r\n                        )\r\n                        OR\r\n                        (\r\n                            \"Type\" = 'CENTRAL_KITCHEN'\r\n                            AND \"FranchiseId\" IS NULL\r\n                            AND \"CentralKitchenId\" IS NOT NULL\r\n                        )");

            migrationBuilder.AddForeignKey(
                name: "FK_ingredient_batches_deliveries_DeliveryId",
                table: "ingredient_batches",
                column: "DeliveryId",
                principalTable: "deliveries",
                principalColumn: "DeliveryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_central_kitchens_CentralKitchenId",
                table: "product_batches",
                column: "CentralKitchenId",
                principalTable: "central_kitchens",
                principalColumn: "CentralKitchenId");

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_deliveries_DeliveryId",
                table: "product_batches",
                column: "DeliveryId",
                principalTable: "deliveries",
                principalColumn: "DeliveryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_franchises_FranchiseId",
                table: "product_batches",
                column: "FranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId");

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches",
                column: "ProductionRunId",
                principalTable: "production_runs",
                principalColumn: "ProductionRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_products_ProductId",
                table: "product_batches",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ingredient_batches_deliveries_DeliveryId",
                table: "ingredient_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_central_kitchens_CentralKitchenId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_deliveries_DeliveryId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_franchises_FranchiseId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_products_ProductId",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "IX_product_batches_DeliveryId",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "UX_product_batches_product_ck_batchcode",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "UX_product_batches_product_franchise_batchcode",
                table: "product_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_batches_onhand_no_delivery",
                table: "product_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_batches_owner",
                table: "product_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_batches_transit_has_delivery",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "IX_ingredient_batches_DeliveryId",
                table: "ingredient_batches");

            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_ck_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_franchise_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_onhand_no_delivery",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_transit_has_delivery",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches");

            migrationBuilder.DropColumn(
                name: "DeliveryId",
                table: "product_batches");

            migrationBuilder.DropColumn(
                name: "IsInTransit",
                table: "product_batches");

            migrationBuilder.DropColumn(
                name: "DeliveryId",
                table: "ingredient_batches");

            migrationBuilder.DropColumn(
                name: "IsInTransit",
                table: "ingredient_batches");

            migrationBuilder.DropColumn(
                name: "IsStockCommitted",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "deliveries");

            migrationBuilder.CreateIndex(
                name: "UX_product_batches_product_ck_batchcode",
                table: "product_batches",
                columns: new[] { "ProductId", "CentralKitchenId", "BatchCode" },
                unique: true,
                filter: "\"CentralKitchenId\" IS NOT NULL AND \"FranchiseId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_product_batches_product_franchise_batchcode",
                table: "product_batches",
                columns: new[] { "ProductId", "FranchiseId", "BatchCode" },
                unique: true,
                filter: "\"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_batches_owner",
                table: "product_batches",
                sql: "\r\n            (\r\n                \"FranchiseId\" IS NOT NULL\r\n                AND \"CentralKitchenId\" IS NULL\r\n            )\r\n            OR\r\n            (\r\n                \"FranchiseId\" IS NULL\r\n                AND \"CentralKitchenId\" IS NOT NULL\r\n            )");

            migrationBuilder.CreateIndex(
                name: "UX_ingredient_batches_ingredient_ck_batchcode",
                table: "ingredient_batches",
                columns: new[] { "IngredientId", "CentralKitchenId", "BatchCode" },
                unique: true,
                filter: "\"CentralKitchenId\" IS NOT NULL AND \"FranchiseId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ingredient_batches_ingredient_franchise_batchcode",
                table: "ingredient_batches",
                columns: new[] { "IngredientId", "FranchiseId", "BatchCode" },
                unique: true,
                filter: "\"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches",
                sql: "\r\n            (\r\n                \"Type\" = 'FRANCHISE'\r\n                AND \"FranchiseId\" IS NOT NULL\r\n                AND \"CentralKitchenId\" IS NULL\r\n            )\r\n            OR\r\n            (\r\n                \"Type\" = 'CENTRAL_KITCHEN'\r\n                AND \"FranchiseId\" IS NULL\r\n                AND \"CentralKitchenId\" IS NOT NULL\r\n            )");

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_central_kitchens_CentralKitchenId",
                table: "product_batches",
                column: "CentralKitchenId",
                principalTable: "central_kitchens",
                principalColumn: "CentralKitchenId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_franchises_FranchiseId",
                table: "product_batches",
                column: "FranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches",
                column: "ProductionRunId",
                principalTable: "production_runs",
                principalColumn: "ProductionRunId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_products_ProductId",
                table: "product_batches",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
