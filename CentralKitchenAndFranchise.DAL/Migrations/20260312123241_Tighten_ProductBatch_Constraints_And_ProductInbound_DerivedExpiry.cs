using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Tighten_ProductBatch_Constraints_And_ProductInbound_DerivedExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches");

            migrationBuilder.DropIndex(
                name: "IX_product_batches_ProductId",
                table: "product_batches");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "product_batches",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "product_batches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

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

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches",
                column: "ProductionRunId",
                principalTable: "production_runs",
                principalColumn: "ProductionRunId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
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

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "product_batches",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "product_batches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_ProductId",
                table: "product_batches",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_product_batches_production_runs_ProductionRunId",
                table: "product_batches",
                column: "ProductionRunId",
                principalTable: "production_runs",
                principalColumn: "ProductionRunId");
        }
    }
}
