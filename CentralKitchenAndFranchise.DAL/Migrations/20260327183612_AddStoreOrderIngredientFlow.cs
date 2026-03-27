using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreOrderIngredientFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_ingredient_items_ingredients_IngredientId",
                table: "delivery_ingredient_items");

            migrationBuilder.DropForeignKey(
                name: "FK_store_order_items_products_ProductId",
                table: "store_order_items");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "delivery_ingredient_items",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "DropReason",
                table: "delivery_ingredient_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDropped",
                table: "delivery_ingredient_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedQuantity",
                table: "delivery_ingredient_items",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "store_order_ingredient_items",
                columns: table => new
                {
                    StoreOrderIngredientItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StoreOrderId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_ingredient_items", x => x.StoreOrderIngredientItemId);
                    table.ForeignKey(
                        name: "FK_store_order_ingredient_items_ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_order_ingredient_items_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalTable: "store_orders",
                        principalColumn: "StoreOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_order_ingredient_items_IngredientId",
                table: "store_order_ingredient_items",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_ingredient_items_StoreOrderId",
                table: "store_order_ingredient_items",
                column: "StoreOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_ingredient_items_ingredients_IngredientId",
                table: "delivery_ingredient_items",
                column: "IngredientId",
                principalTable: "ingredients",
                principalColumn: "IngredientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_store_order_items_products_ProductId",
                table: "store_order_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_ingredient_items_ingredients_IngredientId",
                table: "delivery_ingredient_items");

            migrationBuilder.DropForeignKey(
                name: "FK_store_order_items_products_ProductId",
                table: "store_order_items");

            migrationBuilder.DropTable(
                name: "store_order_ingredient_items");

            migrationBuilder.DropColumn(
                name: "DropReason",
                table: "delivery_ingredient_items");

            migrationBuilder.DropColumn(
                name: "IsDropped",
                table: "delivery_ingredient_items");

            migrationBuilder.DropColumn(
                name: "RequestedQuantity",
                table: "delivery_ingredient_items");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "delivery_ingredient_items",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_ingredient_items_ingredients_IngredientId",
                table: "delivery_ingredient_items",
                column: "IngredientId",
                principalTable: "ingredients",
                principalColumn: "IngredientId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_store_order_items_products_ProductId",
                table: "store_order_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
