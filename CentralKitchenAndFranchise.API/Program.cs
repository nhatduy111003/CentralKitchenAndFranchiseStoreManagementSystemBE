// CentralKitchenAndFranchise.API/Program.cs  (FULL FILE - copy toàn bộ)
using CentralKitchenAndFranchise.API.Middlewares;
using CentralKitchenAndFranchise.BLL.Guards;
using CentralKitchenAndFranchise.BLL.Services.Implementations;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Implementations;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using CentralKitchenAndFranchise.DAL.Seeding;
using CentralKitchenAndFranchise.DAL.UnitOfWork;
using CentralKitchenAndFranchise.DTO.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PdfSharp.Charting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opt =>
    {
        opt.InvalidModelStateResponseFactory = ctx =>
        {
            var fieldErrors = ctx.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => k.Key,
                    v => v.Value!.Errors.Select(e =>
                        string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage
                    ).ToArray()
                );

            var resp = CentralKitchenAndFranchise.DTO.Responses.ApiResponse.Fail(
                message: "Validation failed.",
                errors: null,
                errorCode: "VALIDATION_ERROR",
                fieldErrors: fieldErrors
            );
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(resp);
        };
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();


// CORS: allow FE on http://localhost:8080
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost8080", policy =>
    {
        policy
            .WithOrigins("http://localhost:8080")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Config
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        var key = jwtSection["Key"];

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = ClaimTypes.Role,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
            ClockSkew = TimeSpan.Zero
        };

        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    ctx.Fail("Missing jti");
                    return;
                }

                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var revoked = await db.RevokedTokens.AnyAsync(x => x.Jti == jti);
                if (revoked)
                {
                    ctx.Fail("Token revoked.");
                    return;
                }

                // session control: deny when user is inactive
                var userIdStr = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out var userId))
                {
                    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user is null || !string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Fail("User is inactive.");
                        return;
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CentralKitchenAndFranchise API",
        Version = "v1"
    });

    // JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// DI
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IStoreOrderService, StoreOrderService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFranchiseAccessService, FranchiseAccessService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFranchiseService, FranchiseService>();
builder.Services.AddScoped<ICentralKitchenService, CentralKitchenService>();
builder.Services.AddScoped<IIngredientGuard, IngredientGuard>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductService,ProductService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IStoreCatalogService, StoreCatalogService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IUserWorkAssignmentService, UserWorkAssignmentService>(); 
builder.Services.AddScoped<IDemandService, DemandService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<IManagerDashboardService, ManagerDashboardService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IDashboardScopeService, DashboardScopeService>();
builder.Services.AddScoped<IKitchenDashboardService, KitchenDashboardService>();
builder.Services.AddScoped<ISupplyDashboardService, SupplyDashboardService>();
builder.Services.AddScoped<IStoreDashboardService, StoreDashboardService>();

builder.Services.AddScoped<IProductionPlanService, ProductionPlanService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInventoryHistoryService, InventoryHistoryService>();
builder.Services.AddScoped<IInventoryLedgerWriter, InventoryLedgerWriter>();

builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();

builder.Services.AddScoped<IKitchenOrderService, KitchenOrderService>();
builder.Services.AddScoped<ISupplyOrderService, SupplyOrderService>();
builder.Services.AddScoped<IReceivingService, ReceivingService>();
builder.Services.AddScoped<IInventoryTransferService, InventoryTransferService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IReportExportService, ReportExportService>();

var app = builder.Build();

//auto migrate in development env
var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
var autoMigrateOnStartup = app.Configuration.GetValue<bool>("AUTO_MIGRATE_ON_STARTUP");
var autoSeedOnStartup = app.Configuration.GetValue<bool>("AUTO_SEED_ON_STARTUP");

if (migrateOnly || autoMigrateOnStartup || autoSeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (migrateOnly || autoMigrateOnStartup)
    {
        db.Database.Migrate();
    }

    if (autoSeedOnStartup)
    {
        DbSeeder.Seed(db);
    }

    if (migrateOnly)
    {
        return;
    }
}

if (app.Environment.IsDevelopment())
{
    CentralKitchenAndFranchise.API.Dev.HashTool.Print();

    app.UseSwagger();
    app.UseSwaggerUI();
}

// middleware
app.UseMiddleware<ExceptionMiddleware>();

//routing
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowLocalhost8080");
//authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();