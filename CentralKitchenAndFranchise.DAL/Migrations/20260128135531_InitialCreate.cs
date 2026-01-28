using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_plans",
                columns: table => new
                {
                    delivery_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_plans", x => x.delivery_plan_id);
                });

            migrationBuilder.CreateTable(
                name: "demand_aggregations",
                columns: table => new
                {
                    demand_aggregation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_aggregations", x => x.demand_aggregation_id);
                });

            migrationBuilder.CreateTable(
                name: "franchises",
                columns: table => new
                {
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_franchises", x => x.franchise_id);
                });

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredients", x => x.ingredient_id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.product_id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    contact_info = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.supplier_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.delivery_id);
                    table.ForeignKey(
                        name: "FK_deliveries_delivery_plans_delivery_plan_id",
                        column: x => x.delivery_plan_id,
                        principalTable: "delivery_plans",
                        principalColumn: "delivery_plan_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deliveries_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_plans",
                columns: table => new
                {
                    production_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kitchen_franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_plans", x => x.production_plan_id);
                    table.ForeignKey(
                        name: "FK_production_plans_franchises_kitchen_franchise_id",
                        column: x => x.kitchen_franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.store_order_id);
                    table.ForeignKey(
                        name: "FK_store_orders_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ingredient_batches",
                columns: table => new
                {
                    ingredient_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_code = table.Column<string>(type: "text", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredient_batches", x => x.ingredient_batch_id);
                    table.ForeignKey(
                        name: "FK_ingredient_batches_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ingredient_batches_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "ingredient_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "boms",
                columns: table => new
                {
                    bom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boms", x => x.bom_id);
                    table.ForeignKey(
                        name: "FK_boms_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "demand_items",
                columns: table => new
                {
                    demand_aggregation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_items", x => new { x.demand_aggregation_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_demand_items_demand_aggregations_demand_aggregation_id",
                        column: x => x.demand_aggregation_id,
                        principalTable: "demand_aggregations",
                        principalColumn: "demand_aggregation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_demand_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.recipe_id);
                    table.ForeignKey(
                        name: "FK_recipes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_records",
                columns: table => new
                {
                    sales_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    sold_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_records", x => x.sales_record_id);
                    table.ForeignKey(
                        name: "FK_sales_records_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_records_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_catalog",
                columns: table => new
                {
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_catalog", x => new { x.franchise_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_store_catalog_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_catalog_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                columns: table => new
                {
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    demand_aggregation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocations", x => x.allocation_id);
                    table.ForeignKey(
                        name: "FK_allocations_demand_aggregations_demand_aggregation_id",
                        column: x => x.demand_aggregation_id,
                        principalTable: "demand_aggregations",
                        principalColumn: "demand_aggregation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_allocations_users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.audit_log_id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "support_requests",
                columns: table => new
                {
                    support_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_requests", x => x.support_request_id);
                    table.ForeignKey(
                        name: "FK_support_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_franchises",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_franchises", x => new { x.user_id, x.franchise_id });
                    table.ForeignKey(
                        name: "FK_user_franchises_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_franchises_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_items",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_items", x => new { x.delivery_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_delivery_items_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "delivery_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receiving_reports",
                columns: table => new
                {
                    receiving_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_reports", x => x.receiving_report_id);
                    table.ForeignKey(
                        name: "FK_receiving_reports_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "delivery_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_plan_items",
                columns: table => new
                {
                    production_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_plan_items", x => new { x.production_plan_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_production_plan_items_production_plans_production_plan_id",
                        column: x => x.production_plan_id,
                        principalTable: "production_plans",
                        principalColumn: "production_plan_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_production_plan_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_order_items",
                columns: table => new
                {
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_items", x => new { x.store_order_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_store_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_order_items_store_orders_store_order_id",
                        column: x => x.store_order_id,
                        principalTable: "store_orders",
                        principalColumn: "store_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    inventory_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.inventory_movement_id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_ingredient_batches_ingredient_batch_id",
                        column: x => x.ingredient_batch_id,
                        principalTable: "ingredient_batches",
                        principalColumn: "ingredient_batch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_items",
                columns: table => new
                {
                    bom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_items", x => new { x.bom_id, x.ingredient_id });
                    table.ForeignKey(
                        name: "FK_bom_items_boms_bom_id",
                        column: x => x.bom_id,
                        principalTable: "boms",
                        principalColumn: "bom_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_items_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "ingredient_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "allocation_items",
                columns: table => new
                {
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franchise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_items", x => new { x.allocation_id, x.franchise_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_allocation_items_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalTable: "allocations",
                        principalColumn: "allocation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_allocation_items_franchises_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "franchises",
                        principalColumn: "franchise_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_allocation_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_items_franchise_id",
                table: "allocation_items",
                column: "franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_items_product_id",
                table: "allocation_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_allocations_ApproverUserId",
                table: "allocations",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_allocations_demand_aggregation_id",
                table: "allocations",
                column: "demand_aggregation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_id",
                table: "audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_bom_items_ingredient_id",
                table: "bom_items",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_boms_product_id",
                table: "boms",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_delivery_plan_id",
                table: "deliveries",
                column: "delivery_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_franchise_id",
                table: "deliveries",
                column: "franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_items_product_id",
                table: "delivery_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_demand_items_product_id",
                table: "demand_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_franchise_id",
                table: "ingredient_batches",
                column: "franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_ingredient_id",
                table: "ingredient_batches",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_ingredient_batch_id",
                table: "inventory_movements",
                column: "ingredient_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_plan_items_product_id",
                table: "production_plan_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_plans_kitchen_franchise_id",
                table: "production_plans",
                column: "kitchen_franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_delivery_id",
                table: "receiving_reports",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_product_id",
                table: "recipes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_records_franchise_id",
                table: "sales_records",
                column: "franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_records_product_id",
                table: "sales_records",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_catalog_product_id",
                table: "store_catalog",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_product_id",
                table: "store_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_franchise_id",
                table: "store_orders",
                column: "franchise_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_user_id",
                table: "support_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_franchises_franchise_id",
                table: "user_franchises",
                column: "franchise_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_items");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bom_items");

            migrationBuilder.DropTable(
                name: "delivery_items");

            migrationBuilder.DropTable(
                name: "demand_items");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "production_plan_items");

            migrationBuilder.DropTable(
                name: "receiving_reports");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "sales_records");

            migrationBuilder.DropTable(
                name: "store_catalog");

            migrationBuilder.DropTable(
                name: "store_order_items");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "support_requests");

            migrationBuilder.DropTable(
                name: "user_franchises");

            migrationBuilder.DropTable(
                name: "allocations");

            migrationBuilder.DropTable(
                name: "boms");

            migrationBuilder.DropTable(
                name: "ingredient_batches");

            migrationBuilder.DropTable(
                name: "production_plans");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "store_orders");

            migrationBuilder.DropTable(
                name: "demand_aggregations");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "delivery_plans");

            migrationBuilder.DropTable(
                name: "franchises");
        }
    }
}
