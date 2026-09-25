using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using Serilog;
using ERP.API.Authentication;
using ERP.API.Controllers;
using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Application.Common.Configuration;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Behaviors;
using ERP.Application.Common.Integrations;
using ERP.Application.Common.Documents;
using ERP.Application.Common.Modules;
using ERP.Application.Common.Licensing;
using ERP.Domain.Common.Modules;
using ERP.Application.Common.Reports;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Data;
using ERP.Infrastructure.Data.Interceptors;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with JSON structured logging for production
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NEXTERP-API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Configure Swagger with API versioning
builder.Services.AddSwaggerWithVersioning();

// Configure API Versioning
builder.Services.AddApiVersioningWithExplorer();

// Configure JWT Authentication - Production security settings
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT SecretKey must be configured. Set 'Jwt:SecretKey' in configuration or JWT_SECRET environment variable.");
}

var jwtSettings = new JwtSettings
{
    SecretKey = jwtSecret,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "ERP.System",
    Audience = builder.Configuration["Jwt:Audience"] ?? "ERP.Client",
    // Access token: 15 minutes (production standard - reduced from 60 for security)
    AccessTokenExpirationMinutes = int.Parse(builder.Configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15"),
    // Refresh token: 7 days with rotation enabled
    RefreshTokenExpirationDays = int.Parse(builder.Configuration["Jwt:RefreshTokenExpirationDays"] ?? "7")
};

builder.Services.AddSingleton(jwtSettings);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString()))
            {
                context.Token = context.Request.Cookies["nexterp_token"];
            }
            return Task.CompletedTask;
        }
    };
});

// API Key Authentication for external integrations
var apiKeyOptions = new ERP.API.Authentication.ApiKeyAuthenticationOptions();

// Load API keys from configuration
var apiKeysSection = builder.Configuration.GetSection("ApiKeys");
if (apiKeysSection.Exists())
{
    foreach (var section in apiKeysSection.GetChildren())
    {
        var clientId = section["ClientId"];
        var key = section["Key"];
        var permissions = section.GetSection("Permissions").Get<string[]>() ?? Array.Empty<string>();

        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(key))
        {
            apiKeyOptions.ApiKeys.AddApiKey(clientId, key, permissions);
        }
    }
}

// Add API Key scheme (with fallback for demo)
if (apiKeyOptions.ApiKeys.Count == 0)
{
    // Add demo API key for testing
    apiKeyOptions.ApiKeys.AddApiKey(
        "demo-client",
        builder.Configuration["DemoApiKey"] ?? "demo-api-key-for-testing-only",
        "reports.read", "analytics.dashboard.read");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddScheme<ERP.API.Authentication.ApiKeyAuthenticationOptions, ERP.API.Authentication.ApiKeyAuthenticationHandler>(
    "ApiKey",
    opts => { opts.ApiKeys = apiKeyOptions.ApiKeys; });

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
        name: "postgresql",
        tags: new[] { "db", "postgresql" })
    .AddRedis(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379",
        name: "redis",
        tags: new[] { "cache", "redis" })
    .AddCheck("custom", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireSuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin", "SuperAdmin"));
});

// Add DbContext
builder.Services.AddDbContext<ERPDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsql =>
    {
        npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
        npgsql.CommandTimeout(30);
    });
});

// Data Masking Service for GDPR/PIV compliance
builder.Services.AddScoped<ERP.Application.Common.Security.IDataMaskingService>(sp =>
    new ERP.Application.Common.Security.DataMaskingService());

// Add Redis
var redisConfiguration = ConfigurationOptions.Parse(
    builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");
redisConfiguration.AbortOnConnectFail = false;
var redisMultiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

// Data Protection keys must survive restarts/redeploys and be shared across instances
// (Railway containers are ephemeral), otherwise encrypted PII becomes undecryptable the
// moment the key ring is lost. Persist to Redis instead of the default local file system.
builder.Services.AddDataProtection()
    .SetApplicationName("Nexterp")
    .PersistKeysToStackExchangeRedis(redisMultiplexer, "DataProtection-Keys");
builder.Services.AddSingleton<IEncryptionService, DataProtectionEncryptionService>();

// Add MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(IApplicationDbContext).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(ModuleAuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(PermissionAuthorizationBehavior<,>));
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<IApplicationDbContext>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add Application DbContext
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ERPDbContext>());

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IModuleAccessService, ModuleAccessService>();
builder.Services.AddScoped<ERP.Application.Analytics.Services.INotificationService, ERP.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<IWorkflowService, ERP.Infrastructure.Services.WorkflowService>();
builder.Services.AddScoped<IReportService, ReportService>();

// Brute force protection for login
builder.Services.AddScoped<ILoginRateLimitService, LoginRateLimitService>();

// Global rate limiting service: Redis-backed (shared across instances) outside
// Development, in-memory in Development so `dotnet run` works without Redis running.
// RedisRateLimitService existed but was never registered here, so every environment
// was silently using in-memory limits regardless of this comment's previous claim.
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IRateLimitService, InMemoryRateLimitService>();
else
    builder.Services.AddScoped<IRateLimitService, RedisRateLimitService>();

// Redis caching service
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Export service
builder.Services.AddScoped<IExportService, ExportService>();

// Domain Services
builder.Services.AddScoped<ERP.Domain.Hrm.Services.PayrollCalculationService>();

// External Integration Services
builder.Services.AddScoped<ITaxReportingService, ERP.Infrastructure.Services.Integrations.TaxReportingService>();
builder.Services.AddScoped<IBankTransferService, ERP.Infrastructure.Services.Integrations.BankTransferService>();
builder.Services.AddScoped<INotificationGateway, ERP.Infrastructure.Services.Integrations.NotificationGateway>();
builder.Services.AddScoped<IDocumentTemplateService, ERP.Infrastructure.Services.Documents.DocumentTemplateService>();
builder.Services.AddScoped<IModuleManager, ModuleManager>();

// Licensing Services
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<ILicenseCheckService, LicenseCheckService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddSingleton<ILicenseIntegrityService, LicenseIntegrityService>();
builder.Services.AddScoped<ILicenseAuditService>(sp =>
{
    var auditLogger = new SerilogAuditLogger(sp.GetRequiredService<ILogger<LicenseAuditService>>());
    return new LicenseAuditService(auditLogger);
});

// License Validation Pipeline
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LicenseValidationBehavior<,>));

// Response Compression (Gzip/Brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/xml",
        "text/plain",
        "text/html"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        // For production, allow specific Vercel and Railway domains
        var additionalOrigins = new[]
        {
            "https://nexterp-frontend-production.up.railway.app",
            "https://nextjs-frontend-ivory.vercel.app",
            "https://nextjs-frontend-ok8i1ckcj-rio-wicaksonos-projects.vercel.app",
            "https://rio-wicaksonos-projects.vercel.app",
            "https://api-production-ab1b.up.railway.app",
            "http://localhost:3000",
            "http://localhost:3001",
            "http://localhost:5000"
        };

        var allOrigins = allowedOrigins.Concat(additionalOrigins).ToArray();

        // SECURITY: Remove fallback to allow-all origins - always require explicit origins
        if (allOrigins.Length > 0)
        {
            policy.WithOrigins(allOrigins);
        }
        // else: No additional origins configured - CORS will only use AllowedOrigins from config

        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Total-Count", "X-Page-Count", "X-Correlation-ID");
    });
});

var app = builder.Build();

// Fix missing columns from legacy schema (skip migrations since tables already exist)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ERPDbContext>();
    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();

    try
    {
        // Skip migrations - database already has tables
        // Just add any missing columns
        logger.LogInformation("Ensuring database schema has all required columns...");
        await dbContext.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""RefreshTokenHash"" text;
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""RefreshTokenExpiry"" timestamp with time zone;
            CREATE TABLE IF NOT EXISTS ""OrganizationModules"" (
                ""Id"" uuid NOT NULL,
                ""OrganizationId"" uuid NOT NULL,
                ""ModuleId"" uuid NOT NULL,
                ""ModuleCode"" text NOT NULL DEFAULT '',
                ""ActivatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""ExpiresAt"" timestamp with time zone NULL,
                ""ActivatedBy"" text NULL,
                ""Notes"" text NULL,
                ""ModuleDefinitionId"" uuid NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                CONSTRAINT ""PK_OrganizationModules"" PRIMARY KEY (""Id"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_OrganizationModules_ModuleDefinitionId"" ON ""OrganizationModules"" (""ModuleDefinitionId"");
            ALTER TABLE ""OrganizationModules"" ADD COLUMN IF NOT EXISTS ""ModuleCode"" text NOT NULL DEFAULT '';
            CREATE TABLE IF NOT EXISTS ""LicenseTiers"" (
                ""Id"" uuid NOT NULL,
                ""Code"" text NOT NULL,
                ""DisplayName"" text NOT NULL,
                ""Description"" text NULL,
                ""SortOrder"" integer NOT NULL DEFAULT 0,
                ""MonthlyPrice"" numeric NOT NULL DEFAULT 0,
                ""DefaultMaxUsers"" integer NOT NULL DEFAULT 10,
                ""IsActive"" boolean NOT NULL DEFAULT TRUE,
                ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                CONSTRAINT ""PK_LicenseTiers"" PRIMARY KEY (""Id"")
            );
            CREATE TABLE IF NOT EXISTS ""OrganizationLicenses"" (
                ""Id"" uuid NOT NULL,
                ""OrganizationId"" uuid NOT NULL,
                ""LicenseTierId"" uuid NOT NULL,
                ""StartDate"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""EndDate"" timestamp with time zone NOT NULL,
                ""MaxUsers"" integer NOT NULL DEFAULT 10,
                ""IsAutoRenew"" boolean NOT NULL DEFAULT FALSE,
                ""BillingEmail"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                CONSTRAINT ""PK_OrganizationLicenses"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_OrganizationLicenses_LicenseTiers_LicenseTierId"" FOREIGN KEY (""LicenseTierId"") REFERENCES ""LicenseTiers"" (""Id"") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ""IX_OrganizationLicenses_LicenseTierId"" ON ""OrganizationLicenses"" (""LicenseTierId"");
        ");
        logger.LogInformation("Database schema fixes applied successfully");

        // Baseline license tiers and an active license per organization - unlike the
        // demo org/user/role block below, this isn't "demo data" in the sense that
        // gating protects against (no password involved), it's reference/catalog data
        // that EnableOrganizationModuleCommandHandler's license check requires to
        // exist for ANY organization before it will enable ANY module. Without this,
        // every "Enable" click on the Modules page fails with "No active license
        // found for organization". Ungated and re-checked on every startup, same as
        // the schema fixes above.
        logger.LogInformation("Ensuring license tiers and organization licenses exist...");

        if (!await dbContext.LicenseTiers.AnyAsync(t => t.Code == LicenseTierCodes.Starter))
            dbContext.LicenseTiers.Add(new LicenseTier(LicenseTierCodes.Starter, "Starter", 0m, 10, "Basic modules for small businesses", 1));
        if (!await dbContext.LicenseTiers.AnyAsync(t => t.Code == LicenseTierCodes.Professional))
            dbContext.LicenseTiers.Add(new LicenseTier(LicenseTierCodes.Professional, "Professional", 299m, 25, "For growing businesses", 2));
        if (!await dbContext.LicenseTiers.AnyAsync(t => t.Code == LicenseTierCodes.Enterprise))
            dbContext.LicenseTiers.Add(new LicenseTier(LicenseTierCodes.Enterprise, "Enterprise", 999m, 100, "Full suite with all modules", 3));
        await dbContext.SaveChangesAsync();

        // module-manifest.json's own License.DefaultTier is "starter" - grant that to
        // any organization that doesn't already have an active license, rather than
        // inventing a different default here.
        var defaultTierId = await dbContext.LicenseTiers
            .Where(t => t.Code == LicenseTierCodes.Starter)
            .Select(t => t.Id)
            .FirstAsync();

        var orgIdsWithActiveLicense = await dbContext.OrganizationLicenses
            .Where(l => !l.IsDeleted && l.EndDate >= DateTime.UtcNow)
            .Select(l => l.OrganizationId)
            .ToListAsync();

        var orgsNeedingLicense = await dbContext.Organizations
            .Where(o => !o.IsDeleted && !orgIdsWithActiveLicense.Contains(o.Id))
            .ToListAsync();

        foreach (var org in orgsNeedingLicense)
        {
            dbContext.OrganizationLicenses.Add(new OrganizationLicense(
                org.Id, defaultTierId, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), 10, isAutoRenew: true));
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("License tiers and organization licenses ensured successfully");

        // Demo/sample data (including a default admin account) is only ever seeded in
        // Development, or when explicitly opted into via SEED_DEMO_DATA=true together
        // with an explicit DEMO_PASSWORD. This block used to run — and reset the demo
        // admin's password — on every single startup in every environment, so a
        // production deploy without DEMO_PASSWORD set would silently (re-)provision an
        // admin account with the well-known password "DevPassword2024!".
        var demoPasswordOverride = Environment.GetEnvironmentVariable("DEMO_PASSWORD");
        var seedDemoDataOptIn = Environment.GetEnvironmentVariable("SEED_DEMO_DATA") == "true";
        var shouldSeedDemoData = app.Environment.IsDevelopment()
            || (seedDemoDataOptIn && !string.IsNullOrEmpty(demoPasswordOverride));

        if (!shouldSeedDemoData)
        {
            if (seedDemoDataOptIn)
                logger.LogWarning("SEED_DEMO_DATA is set but DEMO_PASSWORD is not — skipping demo data seeding to avoid provisioning an admin account with a default password.");
            else
                logger.LogInformation("Skipping demo data seeding (not Development and SEED_DEMO_DATA is not set).");
        }
        else
        {
        var demoPasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswordOverride ?? "DevPassword2024!", 12);

        // Ensure demo organization exists
        var demoOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        logger.LogInformation("Ensuring demo data exists...");
        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""Organizations"" (""Id"", ""Name"", ""Code"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            VALUES ('{demoOrgId}', 'Nexterp Demo Corp', 'NEXTERP', TRUE, FALSE, NOW(), NOW())
            ON CONFLICT (""Id"") DO NOTHING;
        ");

        // Ensure admin role exists
        var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""Roles"" (""Id"", ""OrganizationId"", ""Name"", ""Description"", ""IsActive"", ""IsSystemRole"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            VALUES ('{adminRoleId}', '{demoOrgId}', 'Admin', 'System Administrator', TRUE, TRUE, FALSE, NOW(), NOW())
            ON CONFLICT (""Id"") DO NOTHING;
        ");

        // Ensure demo user exists with correct password
        var demoUserId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""Users"" (""Id"", ""OrganizationId"", ""Username"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""Phone"", ""IsActive"", ""IsSuperAdmin"", ""FailedLoginAttempts"", ""LockedUntil"", ""LastLoginAt"", ""LastLoginIp"", ""RefreshTokenHash"", ""RefreshTokenExpiry"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            VALUES ('{demoUserId}', '{demoOrgId}', 'admin', 'admin@nexterp.com', '{demoPasswordHash}', 'System', 'Administrator', NULL, TRUE, TRUE, 0, NULL, NULL, NULL, NULL, NULL, FALSE, NOW(), NOW())
            ON CONFLICT (""Id"") DO UPDATE SET ""PasswordHash"" = EXCLUDED.""PasswordHash"";
        ");

        // Assign Admin role to demo user
        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""UserRoles"" (""Id"", ""UserId"", ""RoleId"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            SELECT '{Guid.NewGuid()}', '{demoUserId}', '{adminRoleId}', FALSE, NOW(), NOW()
            WHERE NOT EXISTS (SELECT 1 FROM ""UserRoles"" WHERE ""UserId"" = '{demoUserId}' AND ""RoleId"" = '{adminRoleId}');
        ");

        // Departments, positions, warehouses, sample customers/suppliers, a full
        // employee roster and organization settings - each independently
        // idempotent, richer than what used to be hardcoded inline here, and
        // shared with DatabaseSeeder.SeedAsync's own from-scratch path.
        await DatabaseSeeder.SeedDepartmentsAndPositionsAsync(dbContext, logger, DateTime.UtcNow);
        await DatabaseSeeder.SeedWarehousesAsync(dbContext, logger, DateTime.UtcNow);
        await DatabaseSeeder.SeedEmployeesAsync(dbContext, logger, DateTime.UtcNow);
        await DatabaseSeeder.SeedCustomersAsync(dbContext, logger, DateTime.UtcNow);
        await DatabaseSeeder.SeedSuppliersAsync(dbContext, logger, DateTime.UtcNow);
        await DatabaseSeeder.SeedOrganizationSettingsAsync(dbContext, logger, DateTime.UtcNow);

        // Seed License Tiers
        var starterTierId = Guid.Parse("00000000-0000-0000-0000-000000000200");
        var proTierId = Guid.Parse("00000000-0000-0000-0000-000000000201");
        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""LicenseTiers"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""SortOrder"", ""MonthlyPrice"", ""DefaultMaxUsers"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
            ('{starterTierId}', 'STARTER', 'Starter', 'For small teams', 1, 99.00, 5, TRUE, FALSE, NOW(), NOW()),
            ('{proTierId}', 'PROFESSIONAL', 'Professional', 'For growing businesses', 2, 299.00, 25, TRUE, FALSE, NOW(), NOW())
            ON CONFLICT (""Id"") DO NOTHING;
        ");

        // Seed Role Permissions for Admin role (all permissions)
        var adminPermissions = new[]
        {
            "admin.users.read", "admin.users.create", "admin.users.update", "admin.users.delete",
            "admin.roles.read", "admin.roles.create", "admin.roles.update", "admin.roles.delete",
            "admin.modules.read", "admin.modules.manage", "admin.settings.read", "admin.settings.update",
            "hrm.employees.read", "hrm.employees.create", "hrm.employees.update", "hrm.employees.delete",
            "hrm.departments.read", "hrm.departments.create", "hrm.departments.update", "hrm.departments.delete",
            "hrm.attendances.read", "hrm.attendances.create", "hrm.attendances.update",
            "hrm.leave.read", "hrm.leave.approve", "hrm.payroll.read", "hrm.payroll.process",
            "hrm.reports.read",
            "inventory.items.read", "inventory.items.create", "inventory.items.update", "inventory.items.delete",
            "inventory.stock.read", "inventory.stock.adjust", "inventory.warehouses.read", "inventory.warehouses.manage",
            "inventory.reports.read",
            "sales.orders.read", "sales.orders.create", "sales.orders.update", "sales.orders.delete",
            "sales.invoices.read", "sales.invoices.create", "sales.invoices.update",
            "sales.customers.read", "sales.customers.manage", "sales.reports.read",
            "purchasing.orders.read", "purchasing.orders.create", "purchasing.orders.update", "purchasing.orders.delete",
            "purchasing.suppliers.read", "purchasing.suppliers.manage", "purchasing.reports.read",
            "accounting.accounts.read", "accounting.accounts.create", "accounting.accounts.update",
            "accounting.journals.read", "accounting.journals.create", "accounting.journals.post",
            "accounting.reports.read", "accounting.reports.financial",
            "projects.read", "projects.create", "projects.update", "projects.delete",
            "projects.tasks.read", "projects.tasks.manage", "projects.reports.read",
            "assets.read", "assets.create", "assets.update", "assets.delete",
            "assets.maintenance.read", "assets.maintenance.schedule", "assets.depreciation.read",
            "quality.inspections.read", "quality.inspections.create", "quality.inspections.update",
            "quality.nc.read", "quality.nc.create", "quality.nc.resolve",
            "analytics.dashboard.read", "analytics.reports.read", "analytics.exports.read"
        };

        foreach (var permission in adminPermissions)
        {
            await dbContext.Database.ExecuteSqlRawAsync($@"
                INSERT INTO ""RolePermissions"" (""Id"", ""RoleId"", ""Permission"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                SELECT '{Guid.NewGuid()}', '{adminRoleId}', '{permission}', FALSE, NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""RolePermissions"" WHERE ""RoleId"" = '{adminRoleId}' AND ""Permission"" = '{permission}');
            ");
        }

        // Seed Organization License - upgrade the demo org to Enterprise so every
        // module can be enabled for demo purposes. The earlier ungated license
        // bootstrap above already grants every org (including this one, once it
        // exists) a Starter license by default, and that runs regardless of
        // SEED_DEMO_DATA - so a plain "insert if none exists" here would never
        // fire once that Starter license is already in place, silently leaving
        // the demo org stuck on Starter forever. UPDATE-then-INSERT instead.
        var enterpriseTierId = await dbContext.LicenseTiers
            .Where(t => t.Code == LicenseTierCodes.Enterprise)
            .Select(t => t.Id)
            .FirstAsync();

        await dbContext.Database.ExecuteSqlRawAsync($@"
            UPDATE ""OrganizationLicenses""
            SET ""LicenseTierId"" = '{enterpriseTierId}', ""EndDate"" = NOW() + INTERVAL '1 year', ""MaxUsers"" = 100, ""UpdatedAt"" = NOW()
            WHERE ""OrganizationId"" = '{demoOrgId}' AND NOT ""IsDeleted"";
        ");

        await dbContext.Database.ExecuteSqlRawAsync($@"
            INSERT INTO ""OrganizationLicenses"" (""Id"", ""OrganizationId"", ""LicenseTierId"", ""StartDate"", ""EndDate"", ""MaxUsers"", ""IsAutoRenew"", ""BillingEmail"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
            SELECT '{Guid.NewGuid()}', '{demoOrgId}', '{enterpriseTierId}', NOW(), NOW() + INTERVAL '1 year', 100, TRUE, 'billing@nexterp.com', FALSE, NOW(), NOW()
            WHERE NOT EXISTS (SELECT 1 FROM ""OrganizationLicenses"" WHERE ""OrganizationId"" = '{demoOrgId}' AND NOT ""IsDeleted"");
        ");

        logger.LogInformation("Demo data ensured successfully");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Schema fix or seeding failed. Continuing anyway...");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithVersioning();
}

app.UseHttpsRedirection();

// Response Compression - must be early in pipeline
app.UseResponseCompression();

// API Audit Logging - logs all requests/responses
app.UseApiAuditLogging();

// Global exception handler - must be early in pipeline
app.UseGlobalExceptionHandler();

app.UseCors();

// Structured request logging with correlation ID
app.UseSerilogRequestLogging();

// Add Correlation ID to all requests
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    await next();
});

// Prometheus metrics
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("service", context => "nexterp-api");
});

app.UseAuthentication();

// Rate limiting middleware — must run after UseAuthentication (not before, as it
// previously was) so context.User.Identity.IsAuthenticated reflects the actual
// caller instead of always being false, which silently capped every request —
// authenticated or not — at the anonymous limit.
app.UseRateLimiting();

app.UseAuthorization();

// Map endpoints
app.MapControllers();

// Enhanced health check endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Just checks if app is running
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true // Checks all dependencies (DB, Redis)
});
app.MapMetrics();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

Log.Information("Starting NEXTERP API on port {Port}", port);

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
