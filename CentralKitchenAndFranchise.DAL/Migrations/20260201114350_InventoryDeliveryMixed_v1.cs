using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InventoryDeliveryMixed_v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "products",
                type: "text",
                nullable: false,
                defaultValue: "FINISHED");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "inventory_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryId",
                table: "inventory_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "inventory_movements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafetyStock",
                table: "ingredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ingredients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<decimal>(
                name: "WasteThreshold",
                table: "ingredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "deliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "deliveries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "FromFranchiseId",
                table: "deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "deliveries",
                type: "text",
                nullable: false,
                defaultValue: "CREATED");

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FranchiseId",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewDataJson",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldDataJson",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_ingredient_items",
                columns: table => new
                {
                    DeliveryIngredientItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_ingredient_items", x => x.DeliveryIngredientItemId);
                    table.ForeignKey(
                        name: "FK_delivery_ingredient_items_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "deliveries",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_ingredient_items_ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_product_items",
                columns: table => new
                {
                    DeliveryProductItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_product_items", x => x.DeliveryProductItemId);
                    table.ForeignKey(
                        name: "FK_delivery_product_items_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "deliveries",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_product_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_batches",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    BatchCode = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpiredAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_batches", x => x.BatchId);
                    table.ForeignKey(
                        name: "FK_product_batches_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_batches_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_movements",
                columns: table => new
                {
                    MovementId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    DeliveryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_movements", x => x.MovementId);
                    table.ForeignKey(
                        name: "FK_product_movements_product_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "product_batches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_DeliveryId",
                table: "inventory_movements",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_FromFranchiseId",
                table: "deliveries",
                column: "FromFranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_FranchiseId",
                table: "audit_logs",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_ingredient_items_DeliveryId_IngredientId",
                table: "delivery_ingredient_items",
                columns: new[] { "DeliveryId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_ingredient_items_IngredientId",
                table: "delivery_ingredient_items",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_product_items_DeliveryId_ProductId",
                table: "delivery_product_items",
                columns: new[] { "DeliveryId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_product_items_ProductId",
                table: "delivery_product_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_FranchiseId",
                table: "product_batches",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_ProductId_BatchCode_FranchiseId",
                table: "product_batches",
                columns: new[] { "ProductId", "BatchCode", "FranchiseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_movements_BatchId",
                table: "product_movements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_product_movements_DeliveryId",
                table: "product_movements",
                column: "DeliveryId");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_franchises_FranchiseId",
                table: "audit_logs",
                column: "FranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId");

            migrationBuilder.AddForeignKey(
                name: "FK_deliveries_franchises_FromFranchiseId",
                table: "deliveries",
                column: "FromFranchiseId",
                principalTable: "franchises",
                principalColumn: "FranchiseId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_franchises_FranchiseId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_deliveries_franchises_FromFranchiseId",
                table: "deliveries");

            migrationBuilder.DropTable(
                name: "delivery_ingredient_items");

            migrationBuilder.DropTable(
                name: "delivery_product_items");

            migrationBuilder.DropTable(
                name: "product_movements");

            migrationBuilder.DropTable(
                name: "product_batches");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_DeliveryId",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_FromFranchiseId",
                table: "deliveries");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_FranchiseId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "DeliveryId",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "SafetyStock",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "WasteThreshold",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "FromFranchiseId",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "FranchiseId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "NewDataJson",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "OldDataJson",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "audit_logs");
        }
    }
}
