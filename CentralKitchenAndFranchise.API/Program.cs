using CentralKitchenAndFranchise.API.Middlewares;
using CentralKitchenAndFranchise.BLL.Services.Implementations;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.BLL.Guards;
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
            .AllowAnyMethod();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
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
            Array.Empty<string>()
        }
    });
});
// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName); // ✅ sửa dòng này
        var key = jwtSection["Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("JWT Key is missing. Please configure Jwt:Key.");

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
            OnAuthenticationFailed = ctx =>
            {
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                // handle default 401 response
                if (!ctx.Response.HasStarted)
                {
                    ctx.HandleResponse();
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    ctx.Response.ContentType = "application/json";
                    var resp = CentralKitchenAndFranchise.DTO.Responses.ApiResponse.Fail(
                        "Unauthorized access. Please login first.",
                        null,
                        "UNAUTHORIZED"
                    );
                    return ctx.Response.WriteAsJsonAsync(resp);
                }
                return Task.CompletedTask;
            },
            OnForbidden = ctx =>
            {
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    ctx.Response.ContentType = "application/json";
                    var resp = CentralKitchenAndFranchise.DTO.Responses.ApiResponse.Fail(
                        "Forbidden access. You do not have permission to access this resource.",
                        null,
                        "FORBIDDEN"
                    );
                    return ctx.Response.WriteAsJsonAsync(resp);
                }
                return Task.CompletedTask;
            },

            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    ctx.Fail("Missing jti");
                    return;
                }

                var repo = ctx.HttpContext.RequestServices.GetRequiredService<IRevokedTokenRepository>();
                if (await repo.IsRevokedAsync(jti))
                {
                    ctx.Fail("Token revoked");
                }
            }
        };
    });

builder.Services.AddAuthorization();
// Config
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// EF Core - CHỈ 1 LẦN
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// DAL DI
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

// BLL DI
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFranchiseAccessService, FranchiseAccessService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFranchiseService, FranchiseService>();
builder.Services.AddScoped<IIngredientGuard, IngredientGuard>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductService,
    ProductService>();
builder.Services.AddScoped<IStoreCatalogService, StoreCatalogService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IUserFranchiseService, UserFranchiseService>();
builder.Services.AddScoped<IDemandService, DemandService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();

var app = builder.Build();

// Auto migrate + seed (DB đầy đủ)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
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

// Enable CORS (must be before MapControllers)
app.UseCors("AllowLocalhost8080");
//authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
