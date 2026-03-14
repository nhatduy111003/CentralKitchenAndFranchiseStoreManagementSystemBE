using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CentralKitchenAndFranchise.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Rebuild_Init_Clean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "central_kitchens",
                columns: table => new
                {
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_central_kitchens", x => x.CentralKitchenId);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: false),
                    ProductType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "revoked_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Jti = table.Column<string>(type: "text", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revoked_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContactInfo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.SupplierId);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    SystemSettingId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.SystemSettingId);
                });

            migrationBuilder.CreateTable(
                name: "demand_aggregations",
                columns: table => new
                {
                    DemandAggregationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_aggregations", x => x.DemandAggregationId);
                    table.ForeignKey(
                        name: "FK_demand_aggregations_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "franchises",
                columns: table => new
                {
                    FranchiseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_franchises", x => x.FranchiseId);
                    table.ForeignKey(
                        name: "FK_franchises_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_plans",
                columns: table => new
                {
                    ProductionPlanId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_plans", x => x.ProductionPlanId);
                    table.ForeignKey(
                        name: "FK_production_plans_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boms",
                columns: table => new
                {
                    BomId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boms", x => x.BomId);
                    table.ForeignKey(
                        name: "FK_boms_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.RecipeId);
                    table.ForeignKey(
                        name: "FK_recipes_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_users_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "numeric", nullable: false),
                    WasteThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredients", x => x.IngredientId);
                    table.ForeignKey(
                        name: "FK_ingredients_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                columns: table => new
                {
                    AllocationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DemandAggregationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocations", x => x.AllocationId);
                    table.ForeignKey(
                        name: "FK_allocations_demand_aggregations_DemandAggregationId",
                        column: x => x.DemandAggregationId,
                        principalTable: "demand_aggregations",
                        principalColumn: "DemandAggregationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "demand_items",
                columns: table => new
                {
                    DemandItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DemandAggregationId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_items", x => x.DemandItemId);
                    table.ForeignKey(
                        name: "FK_demand_items_demand_aggregations_DemandAggregationId",
                        column: x => x.DemandAggregationId,
                        principalTable: "demand_aggregations",
                        principalColumn: "DemandAggregationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_demand_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_plans",
                columns: table => new
                {
                    DeliveryPlanId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_plans", x => x.DeliveryPlanId);
                    table.ForeignKey(
                        name: "FK_delivery_plans_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_plans_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_records",
                columns: table => new
                {
                    SalesRecordId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    SoldAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_records", x => x.SalesRecordId);
                    table.ForeignKey(
                        name: "FK_sales_records_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_records_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_catalog",
                columns: table => new
                {
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_catalog", x => new { x.FranchiseId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_store_catalog_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    StoreOrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.StoreOrderId);
                    table.ForeignKey(
                        name: "FK_store_orders_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_plan_items",
                columns: table => new
                {
                    ProductionPlanItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionPlanId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_plan_items", x => x.ProductionPlanItemId);
                    table.ForeignKey(
                        name: "FK_production_plan_items_production_plans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "production_plans",
                        principalColumn: "ProductionPlanId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_production_plan_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_runs",
                columns: table => new
                {
                    ProductionRunId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionPlanId = table.Column<int>(type: "integer", nullable: false),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: false),
                    RunCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_runs", x => x.ProductionRunId);
                    table.ForeignKey(
                        name: "FK_production_runs_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_runs_production_plans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "production_plans",
                        principalColumn: "ProductionPlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    FranchiseId = table.Column<int>(type: "integer", nullable: true),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityName = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    OldDataJson = table.Column<string>(type: "text", nullable: true),
                    NewDataJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_audit_logs_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId");
                    table.ForeignKey(
                        name: "FK_audit_logs_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId");
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "support_requests",
                columns: table => new
                {
                    SupportRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_requests", x => x.SupportRequestId);
                    table.ForeignKey(
                        name: "FK_support_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_work_assignments",
                columns: table => new
                {
                    UserWorkAssignmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FranchiseId = table.Column<int>(type: "integer", nullable: true),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_work_assignments", x => x.UserWorkAssignmentId);
                    table.CheckConstraint("CK_user_work_assignments_owner", "\r\n    (\r\n        (\"AssignmentType\" = 'FRANCHISE' AND \"FranchiseId\" IS NOT NULL AND \"CentralKitchenId\" IS NULL)\r\n        OR\r\n        (\"AssignmentType\" = 'CENTRAL_KITCHEN' AND \"FranchiseId\" IS NULL AND \"CentralKitchenId\" IS NOT NULL)\r\n    )");
                    table.ForeignKey(
                        name: "FK_user_work_assignments_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_work_assignments_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_work_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_items",
                columns: table => new
                {
                    BomItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BomId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_items", x => x.BomItemId);
                    table.ForeignKey(
                        name: "FK_bom_items_boms_BomId",
                        column: x => x.BomId,
                        principalTable: "boms",
                        principalColumn: "BomId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_items_ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ingredient_batches",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FranchiseId = table.Column<int>(type: "integer", nullable: true),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    BatchCode = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredient_batches", x => x.BatchId);
                    table.CheckConstraint("CK_ingredient_batches_type_owner", "\r\n                        (\r\n                            \"Type\" = 'FRANCHISE'\r\n                            AND \"FranchiseId\" IS NOT NULL\r\n                            AND \"CentralKitchenId\" IS NULL\r\n                        )\r\n                        OR\r\n                        (\r\n                            \"Type\" = 'CENTRAL_KITCHEN'\r\n                            AND \"FranchiseId\" IS NULL\r\n                            AND \"CentralKitchenId\" IS NOT NULL\r\n                        )");
                    table.ForeignKey(
                        name: "FK_ingredient_batches_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ingredient_batches_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ingredient_batches_ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "allocation_items",
                columns: table => new
                {
                    AllocationItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllocationId = table.Column<int>(type: "integer", nullable: false),
                    FranchiseId = table.Column<int>(type: "integer", nullable: false),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_items", x => x.AllocationItemId);
                    table.ForeignKey(
                        name: "FK_allocation_items_allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "allocations",
                        principalColumn: "AllocationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_allocation_items_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_allocation_items_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_allocation_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    DeliveryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryPlanId = table.Column<int>(type: "integer", nullable: false),
                    FromCentralKitchenId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.DeliveryId);
                    table.ForeignKey(
                        name: "FK_deliveries_central_kitchens_FromCentralKitchenId",
                        column: x => x.FromCentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliveries_delivery_plans_DeliveryPlanId",
                        column: x => x.DeliveryPlanId,
                        principalTable: "delivery_plans",
                        principalColumn: "DeliveryPlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_order_items",
                columns: table => new
                {
                    StoreOrderItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StoreOrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_items", x => x.StoreOrderItemId);
                    table.ForeignKey(
                        name: "FK_store_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_order_items_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalTable: "store_orders",
                        principalColumn: "StoreOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_batches",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductionRunId = table.Column<int>(type: "integer", nullable: true),
                    FranchiseId = table.Column<int>(type: "integer", nullable: true),
                    CentralKitchenId = table.Column<int>(type: "integer", nullable: true),
                    BatchCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_batches", x => x.BatchId);
                    table.CheckConstraint("CK_product_batches_owner", "\r\n            (\r\n                \"FranchiseId\" IS NOT NULL\r\n                AND \"CentralKitchenId\" IS NULL\r\n            )\r\n            OR\r\n            (\r\n                \"FranchiseId\" IS NULL\r\n                AND \"CentralKitchenId\" IS NOT NULL\r\n            )");
                    table.ForeignKey(
                        name: "FK_product_batches_central_kitchens_CentralKitchenId",
                        column: x => x.CentralKitchenId,
                        principalTable: "central_kitchens",
                        principalColumn: "CentralKitchenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_batches_franchises_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "franchises",
                        principalColumn: "FranchiseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_batches_production_runs_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "production_runs",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_batches_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.MovementId);
                    table.ForeignKey(
                        name: "FK_inventory_movements_ingredient_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ingredient_batches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "receiving_reports",
                columns: table => new
                {
                    ReceivingReportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_reports", x => x.ReceivingReportId);
                    table.ForeignKey(
                        name: "FK_receiving_reports_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "deliveries",
                        principalColumn: "DeliveryId",
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_allocation_items_AllocationId",
                table: "allocation_items",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_items_CentralKitchenId",
                table: "allocation_items",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_items_FranchiseId",
                table: "allocation_items",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_items_ProductId",
                table: "allocation_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_allocations_DemandAggregationId",
                table: "allocations",
                column: "DemandAggregationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CentralKitchenId",
                table: "audit_logs",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_FranchiseId",
                table: "audit_logs",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_items_BomId",
                table: "bom_items",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_items_IngredientId",
                table: "bom_items",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_boms_ProductId",
                table: "boms",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_DeliveryPlanId",
                table: "deliveries",
                column: "DeliveryPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_FromCentralKitchenId",
                table: "deliveries",
                column: "FromCentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_ingredient_items_DeliveryId",
                table: "delivery_ingredient_items",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_ingredient_items_IngredientId",
                table: "delivery_ingredient_items",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_plans_CentralKitchenId",
                table: "delivery_plans",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_plans_FranchiseId",
                table: "delivery_plans",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_product_items_DeliveryId",
                table: "delivery_product_items",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_product_items_ProductId",
                table: "delivery_product_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_demand_aggregations_CentralKitchenId_PlanDate",
                table: "demand_aggregations",
                columns: new[] { "CentralKitchenId", "PlanDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demand_items_DemandAggregationId",
                table: "demand_items",
                column: "DemandAggregationId");

            migrationBuilder.CreateIndex(
                name: "IX_demand_items_ProductId",
                table: "demand_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_franchises_CentralKitchenId",
                table: "franchises",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_CentralKitchenId",
                table: "ingredient_batches",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_FranchiseId",
                table: "ingredient_batches",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_batches_IngredientId",
                table: "ingredient_batches",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ingredients_SupplierId",
                table: "ingredients",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_BatchId",
                table: "inventory_movements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_CentralKitchenId",
                table: "product_batches",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_FranchiseId",
                table: "product_batches",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_ProductionRunId",
                table: "product_batches",
                column: "ProductionRunId");

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

            migrationBuilder.CreateIndex(
                name: "IX_product_movements_BatchId",
                table: "product_movements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_production_plan_items_ProductId",
                table: "production_plan_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_production_plan_items_ProductionPlanId",
                table: "production_plan_items",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_production_plans_CentralKitchenId",
                table: "production_plans",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_production_runs_CentralKitchenId",
                table: "production_runs",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_production_runs_ProductionPlanId",
                table: "production_runs",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_reports_DeliveryId",
                table: "receiving_reports",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_ProductId",
                table: "recipes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_PermissionId",
                table: "role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_records_FranchiseId",
                table: "sales_records",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_records_ProductId",
                table: "sales_records",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_store_catalog_ProductId",
                table: "store_catalog",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_ProductId",
                table: "store_order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_StoreOrderId",
                table: "store_order_items",
                column: "StoreOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_FranchiseId",
                table: "store_orders",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_UserId",
                table: "support_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_work_assignments_CentralKitchenId",
                table: "user_work_assignments",
                column: "CentralKitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_user_work_assignments_FranchiseId",
                table: "user_work_assignments",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_user_work_assignments_UserId",
                table: "user_work_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                table: "users",
                column: "RoleId");
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
                name: "delivery_ingredient_items");

            migrationBuilder.DropTable(
                name: "delivery_product_items");

            migrationBuilder.DropTable(
                name: "demand_items");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "product_movements");

            migrationBuilder.DropTable(
                name: "production_plan_items");

            migrationBuilder.DropTable(
                name: "receiving_reports");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "revoked_tokens");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "sales_records");

            migrationBuilder.DropTable(
                name: "store_catalog");

            migrationBuilder.DropTable(
                name: "store_order_items");

            migrationBuilder.DropTable(
                name: "support_requests");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "user_work_assignments");

            migrationBuilder.DropTable(
                name: "allocations");

            migrationBuilder.DropTable(
                name: "boms");

            migrationBuilder.DropTable(
                name: "ingredient_batches");

            migrationBuilder.DropTable(
                name: "product_batches");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "store_orders");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "demand_aggregations");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "production_runs");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "delivery_plans");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "production_plans");

            migrationBuilder.DropTable(
                name: "franchises");

            migrationBuilder.DropTable(
                name: "central_kitchens");
        }
    }
}
