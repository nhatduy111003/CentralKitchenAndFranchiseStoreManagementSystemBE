using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientBatchUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ingredient_batches_IngredientId",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_ck_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropIndex(
                name: "UX_ingredient_batches_ingredient_franchise_batchcode",
                table: "ingredient_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_IngredientId",
                table: "ingredient_batches",
                column: "IngredientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingredient_batches_type_owner",
                table: "ingredient_batches",
                sql: "\r\n                        (\r\n                            \"Type\" = 'FRANCHISE'\r\n                            AND \"FranchiseId\" IS NOT NULL\r\n                            AND \"CentralKitchenId\" IS NULL\r\n                        )\r\n                        OR\r\n                        (\r\n                            \"Type\" = 'CENTRAL_KITCHEN'\r\n                            AND \"FranchiseId\" IS NULL\r\n                            AND \"CentralKitchenId\" IS NOT NULL\r\n                        )");
        }
    }
}
