#pragma warning disable EF1002 // Warning for raw SQL in seeder - all values are hardcoded constants
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Data;

/// <summary>
/// Seeds the database with demo/reference data. Each Seed*Async method is
/// independently idempotent (guarded by its own existence check) and callable
/// on its own - Program.cs calls only the sections that don't overlap with its
/// own Organization/User/License/Module seeding, while SeedAsync below remains
/// the all-in-one entry point for a from-scratch database.
/// </summary>
public static class DatabaseSeeder
{
    public static readonly Guid DemoOrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Departments
    public static readonly Guid EngineeringDeptId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid HrDeptId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    public static readonly Guid FinanceDeptId = Guid.Parse("00000000-0000-0000-0000-000000000012");
    public static readonly Guid SalesDeptId = Guid.Parse("00000000-0000-0000-0000-000000000013");
    public static readonly Guid WarehouseDeptId = Guid.Parse("00000000-0000-0000-0000-000000000014");

    // Positions
    public static readonly Guid EngineeringPositionId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    public static readonly Guid HrPositionId = Guid.Parse("00000000-0000-0000-0000-000000000021");
    public static readonly Guid ManagerPositionId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    public static readonly Guid FinancePositionId = Guid.Parse("00000000-0000-0000-0000-000000000023");
    public static readonly Guid SalesPositionId = Guid.Parse("00000000-0000-0000-0000-000000000024");
    public static readonly Guid WarehousePositionId = Guid.Parse("00000000-0000-0000-0000-000000000025");

    // Warehouses
    public static readonly Guid MainWarehouseId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    public static readonly Guid SecondaryWarehouseId = Guid.Parse("00000000-0000-0000-0000-000000000031");

    // Demo User ID
    public static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000100");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    // License Tiers
    public static readonly Guid StarterTierId = Guid.Parse("00000000-0000-0000-0000-000000000200");
    public static readonly Guid ProfessionalTierId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid EnterpriseTierId = Guid.Parse("00000000-0000-0000-0000-000000000202");

    // Module Definitions (the legacy GUID-keyed Modules/ModuleAccessService system -
    // not the manifest-driven ModuleConfigurationLoader system the frontend Modules
    // page and Program.cs's own license/module bootstrap actually use)
    public static readonly Guid SalesModuleId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    public static readonly Guid InventoryModuleId = Guid.Parse("00000000-0000-0000-0001-000000000002");
    public static readonly Guid PurchasingModuleId = Guid.Parse("00000000-0000-0000-0001-000000000003");
    public static readonly Guid AccountingModuleId = Guid.Parse("00000000-0000-0000-0001-000000000004");
    public static readonly Guid HrmModuleId = Guid.Parse("00000000-0000-0000-0001-000000000005");
    public static readonly Guid ProjectsModuleId = Guid.Parse("00000000-0000-0000-0001-000000000006");
    public static readonly Guid QualityModuleId = Guid.Parse("00000000-0000-0000-0001-000000000007");
    public static readonly Guid AnalyticsModuleId = Guid.Parse("00000000-0000-0000-0001-000000000008");
    public static readonly Guid AssetsModuleId = Guid.Parse("00000000-0000-0000-0001-000000000009");

    /// <summary>
    /// Runs a seed section's inserts inside a transaction so a failure partway
    /// through (e.g. a dropped connection on employee #5 of 8) rolls back
    /// everything the section wrote. Without this, the surrounding AnyAsync()
    /// guard would see the partial rows on the next startup and skip the
    /// section entirely, permanently leaving it half-seeded.
    /// </summary>
    private static async Task RunInTransactionAsync(ERPDbContext context, Func<Task> work)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await work();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Full from-scratch seed: organization, admin user, license/module catalog
    /// (legacy GUID-keyed system) and all reference/demo data below. Not currently
    /// called anywhere - Program.cs calls the individual Seed*Async sections it
    /// needs instead, since it already owns Organization/User/License seeding
    /// using the manifest-driven module system.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ERPDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()!.CreateLogger("DatabaseSeeder");
        var now = DateTime.UtcNow;

        if (!await context.Organizations.AnyAsync())
        {
            logger.LogInformation("Seeding organization...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Organizations"" (""Id"", ""Name"", ""Code"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES ({DemoOrganizationId}, 'Nexterp Demo Corp', 'NEXTERP', TRUE, FALSE, {now}, {now})");
            });
        }

        if (!await context.Roles.AnyAsync())
        {
            logger.LogInformation("Seeding roles...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Roles"" (""Id"", ""OrganizationId"", ""Name"", ""Description"", ""IsActive"", ""IsSystemRole"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({AdminRoleId}, {DemoOrganizationId}, 'Admin', 'System Administrator', TRUE, TRUE, FALSE, {now}, {now})");
            });
        }

        if (!await context.Users.AnyAsync())
        {
            logger.LogInformation("Seeding demo user...");
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Users"" (""Id"", ""OrganizationId"", ""Username"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""Phone"", ""IsActive"", ""IsSuperAdmin"", ""FailedLoginAttempts"", ""LockedUntil"", ""LastLoginAt"", ""LastLoginIp"", ""RefreshTokenHash"", ""RefreshTokenExpiry"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({DemoUserId}, {DemoOrganizationId}, 'admin', 'admin@nexterp.com', {passwordHash}, 'System', 'Administrator', NULL, TRUE, TRUE, 0, NULL, NULL, NULL, NULL, NULL, FALSE, {now}, {now})");

                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""UserRoles"" (""Id"", ""UserId"", ""RoleId"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({Guid.NewGuid()}, {DemoUserId}, {AdminRoleId}, FALSE, {now}, {now})");
            });
        }

        await SeedDepartmentsAndPositionsAsync(context, logger, now);
        await SeedEmployeesAsync(context, logger, now);
        await SeedWarehousesAsync(context, logger, now);
        await SeedCustomersAsync(context, logger, now);
        await SeedSuppliersAsync(context, logger, now);

        // ============ LICENSE TIERS (legacy GUID-keyed system) ============
        if (!await context.LicenseTiers.AnyAsync())
        {
            logger.LogInformation("Seeding license tiers...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""LicenseTiers"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""MonthlyPrice"", ""DefaultMaxUsers"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({StarterTierId}, 'STARTER', 'Starter', 'Basic ERP package with core modules', 500000, 10, 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""LicenseTiers"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""MonthlyPrice"", ""DefaultMaxUsers"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({ProfessionalTierId}, 'PROFESSIONAL', 'Professional', 'Full ERP with HRM and Accounting', 1500000, 50, 2, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""LicenseTiers"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""MonthlyPrice"", ""DefaultMaxUsers"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({EnterpriseTierId}, 'ENTERPRISE', 'Enterprise', 'Complete ERP with all modules', 3000000, 200, 3, TRUE, FALSE, {now}, {now})");
            });
        }

        // ============ MODULE DEFINITIONS (legacy GUID-keyed system) ============
        if (!await context.Modules.AnyAsync())
        {
            logger.LogInformation("Seeding module definitions...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({SalesModuleId}, 'SALES', 'Sales Management', 'Customer management, quotes, orders, and invoices', 0, FALSE, 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({InventoryModuleId}, 'INVENTORY', 'Inventory Management', 'Stock management, warehouses, batch tracking', 0, FALSE, 2, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({PurchasingModuleId}, 'PURCHASING', 'Purchasing', 'Supplier management, purchase orders, goods receipt', 0, FALSE, 3, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({AccountingModuleId}, 'ACCOUNTING', 'Accounting', 'Chart of accounts, journals, financial reports', 1, TRUE, 4, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({HrmModuleId}, 'HRM', 'Human Resource Management', 'Employee management, attendance, leave, payroll', 1, TRUE, 5, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({ProjectsModuleId}, 'PROJECTS', 'Project Management', 'Project planning, task tracking, Gantt charts', 2, TRUE, 6, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({QualityModuleId}, 'QUALITY', 'Quality Management', 'Inspections, NCR, CAPA management', 2, TRUE, 7, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({AnalyticsModuleId}, 'ANALYTICS', 'Analytics & Reporting', 'Real-time dashboards, KPI tracking', 2, TRUE, 8, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Modules"" (""Id"", ""Code"", ""DisplayName"", ""Description"", ""Category"", ""IsPremium"", ""SortOrder"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({AssetsModuleId}, 'ASSETS', 'Asset Management', 'Fixed assets, depreciation, maintenance tracking', 2, TRUE, 9, TRUE, FALSE, {now}, {now})");
            });
        }

        // ============ MODULE PERMISSIONS ============
        if (!await context.ModulePermissions.AnyAsync())
        {
            logger.LogInformation("Seeding module permissions...");

            // Permission strings match the [RequiresPermission("module.resource.action")]
            // attributes enforced by PermissionAuthorizationBehavior on each module's
            // commands/queries, covering all 9 licensable modules.
            var permissions = new (Guid ModuleId, string Permission, string Description)[]
            {
                (SalesModuleId, "sales.customers.read", "View customers"),
                (SalesModuleId, "sales.customers.create", "Create customers"),
                (SalesModuleId, "sales.customers.update", "Update customers"),
                (SalesModuleId, "sales.customers.delete", "Delete customers"),
                (SalesModuleId, "sales.orders.read", "View sales orders"),
                (SalesModuleId, "sales.orders.create", "Create sales orders/quotes"),
                (SalesModuleId, "sales.orders.submit", "Submit sales orders for approval"),
                (SalesModuleId, "sales.orders.approve", "Approve sales orders"),
                (SalesModuleId, "sales.orders.cancel", "Cancel sales orders"),

                (PurchasingModuleId, "purchasing.suppliers.read", "View suppliers"),
                (PurchasingModuleId, "purchasing.suppliers.create", "Create suppliers"),
                (PurchasingModuleId, "purchasing.suppliers.update", "Update suppliers"),
                (PurchasingModuleId, "purchasing.suppliers.delete", "Delete suppliers"),
                (PurchasingModuleId, "purchasing.orders.read", "View purchase orders"),
                (PurchasingModuleId, "purchasing.orders.create", "Create purchase orders"),
                (PurchasingModuleId, "purchasing.orders.submit", "Submit purchase orders for approval"),
                (PurchasingModuleId, "purchasing.orders.approve", "Approve purchase orders"),
                (PurchasingModuleId, "purchasing.orders.cancel", "Cancel purchase orders"),

                (InventoryModuleId, "inventory.warehouses.read", "View warehouses"),
                (InventoryModuleId, "inventory.warehouses.create", "Create warehouses"),
                (InventoryModuleId, "inventory.items.read", "View stock items"),
                (InventoryModuleId, "inventory.items.create", "Create stock items"),

                (AccountingModuleId, "accounting.accounts.read", "View chart of accounts"),
                (AccountingModuleId, "accounting.accounts.create", "Create accounts"),
                (AccountingModuleId, "accounting.accounts.update", "Update accounts"),
                (AccountingModuleId, "accounting.accounts.delete", "Delete accounts"),
                (AccountingModuleId, "accounting.journals.read", "View journal entries"),
                (AccountingModuleId, "accounting.journals.create", "Create journal entries"),
                (AccountingModuleId, "accounting.journals.submit", "Submit journal entries for approval"),
                (AccountingModuleId, "accounting.journals.approve", "Approve journal entries"),
                (AccountingModuleId, "accounting.journals.post", "Post approved journal entries"),
                (AccountingModuleId, "accounting.journals.reverse", "Reverse posted journal entries"),
                (AccountingModuleId, "accounting.journals.cancel", "Cancel journal entries"),

                (HrmModuleId, "hrm.employees.read", "View employees"),
                (HrmModuleId, "hrm.employees.create", "Add new employees"),
                (HrmModuleId, "hrm.employees.update", "Update employee data"),
                (HrmModuleId, "hrm.employees.delete", "Delete employees"),
                (HrmModuleId, "hrm.departments.read", "View department statistics"),
                (HrmModuleId, "hrm.departments.create", "Create departments"),
                (HrmModuleId, "hrm.departments.update", "Update departments"),
                (HrmModuleId, "hrm.positions.create", "Create positions"),
                (HrmModuleId, "hrm.positions.update", "Update positions"),
                (HrmModuleId, "hrm.attendance.read", "View attendance records"),
                (HrmModuleId, "hrm.attendance.checkin", "Check in / check out"),
                (HrmModuleId, "hrm.attendance.manage", "Manually record/override attendance"),
                (HrmModuleId, "hrm.leave.read", "View leave requests and balances"),
                (HrmModuleId, "hrm.leave.request", "Submit/cancel own leave requests"),
                (HrmModuleId, "hrm.leave.approve", "Approve leave requests"),
                (HrmModuleId, "hrm.leave.manage", "Manage leave balances"),
                (HrmModuleId, "hrm.overtime.request", "Submit/cancel overtime requests"),
                (HrmModuleId, "hrm.overtime.approve", "Approve overtime requests"),
                (HrmModuleId, "hrm.payroll.view", "View payroll data and payslips"),
                (HrmModuleId, "hrm.payroll.manage", "Create/approve/pay/delete payroll"),
                (HrmModuleId, "hrm.dashboard.read", "View HR dashboard"),

                (ProjectsModuleId, "projects.projects.read", "View projects"),
                (ProjectsModuleId, "projects.projects.create", "Create projects"),
                (ProjectsModuleId, "projects.tasks.read", "View project tasks"),
                (ProjectsModuleId, "projects.tasks.create", "Create project tasks"),
                (ProjectsModuleId, "projects.tasks.update", "Update project task status"),

                (QualityModuleId, "quality.inspections.read", "View quality inspections"),
                (QualityModuleId, "quality.inspections.create", "Create quality inspections"),
                (QualityModuleId, "quality.inspections.update", "Complete quality inspections"),
                (QualityModuleId, "quality.ncr.read", "View non-conformance reports"),
                (QualityModuleId, "quality.ncr.create", "Create non-conformance reports"),
                (QualityModuleId, "quality.ncr.update", "Resolve non-conformance reports"),

                (AnalyticsModuleId, "analytics.audit.read", "View audit logs"),
                (AnalyticsModuleId, "analytics.audit.create", "Write audit log entries"),
                (AnalyticsModuleId, "analytics.notifications.create", "Send notifications to users"),

                (AssetsModuleId, "assets.assets.read", "View fixed assets"),
                (AssetsModuleId, "assets.assets.create", "Create fixed assets"),
                (AssetsModuleId, "assets.assets.update", "Update fixed assets"),
                (AssetsModuleId, "assets.maintenance.create", "Schedule asset maintenance"),
            };

            await RunInTransactionAsync(context, async () =>
            {
                foreach (var (moduleId, permission, description) in permissions)
                {
                    await context.Database.ExecuteSqlAsync($@"
                        INSERT INTO ""ModulePermissions"" (""Id"", ""ModuleId"", ""Permission"", ""Description"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                        ({Guid.NewGuid()}, {moduleId}, {permission}, {description}, FALSE, {now}, {now})");
                }
            });
        }

        // ============ ORGANIZATION LICENSE (Demo gets Enterprise, legacy system) ============
        if (!await context.OrganizationLicenses.AnyAsync())
        {
            logger.LogInformation("Seeding demo organization license...");
            var licenseEndDate = now.AddYears(1);
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""OrganizationLicenses"" (""Id"", ""OrganizationId"", ""LicenseTierId"", ""StartDate"", ""EndDate"", ""MaxUsers"", ""BillingEmail"", ""IsAutoRenew"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({Guid.NewGuid()}, {DemoOrganizationId}, {EnterpriseTierId}, {now}, {licenseEndDate}, 50, 'billing@nexterp.com', FALSE, FALSE, {now}, {now})");
            });
        }

        // ============ ORGANIZATION MODULES (Demo gets all modules, legacy system) ============
        if (!await context.OrganizationModules.AnyAsync())
        {
            logger.LogInformation("Seeding demo organization modules...");
            await RunInTransactionAsync(context, async () =>
            {
                foreach (var moduleId in new[] { SalesModuleId, InventoryModuleId, PurchasingModuleId, AccountingModuleId, HrmModuleId, ProjectsModuleId, QualityModuleId, AnalyticsModuleId, AssetsModuleId })
                {
                    await context.Database.ExecuteSqlAsync($@"
                        INSERT INTO ""OrganizationModules"" (""Id"", ""OrganizationId"", ""ModuleId"", ""ActivatedAt"", ""ActivatedBy"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                        ({Guid.NewGuid()}, {DemoOrganizationId}, {moduleId}, {now}, 'SYSTEM', FALSE, {now}, {now})");
                }
            });
        }

        await SeedOrganizationSettingsAsync(context, logger, now);

        logger.LogInformation("Demo data seeding complete!");
    }

    // ============ DEPARTMENTS & POSITIONS ============
    // IgnoreQueryFilters: seeding runs outside an authenticated request, so the
    // global tenant filter has no organization to scope to and would otherwise
    // make this existence check always return false.
    public static async Task SeedDepartmentsAndPositionsAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (!await context.Departments.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding departments...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Departments"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({EngineeringDeptId}, {DemoOrganizationId}, 'Engineering', 'ENG', 'Software Engineering Department', TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Departments"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({HrDeptId}, {DemoOrganizationId}, 'Human Resources', 'HR', 'HR Management Department', TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Departments"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({FinanceDeptId}, {DemoOrganizationId}, 'Finance', 'FIN', 'Finance & Accounting Department', TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Departments"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({SalesDeptId}, {DemoOrganizationId}, 'Sales', 'SLS', 'Sales & Marketing Department', TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Departments"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({WarehouseDeptId}, {DemoOrganizationId}, 'Warehouse', 'WHS', 'Warehouse & Logistics Department', TRUE, FALSE, {now}, {now})");
            });
        }

        if (!await context.Positions.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding positions...");
            await RunInTransactionAsync(context, async () =>
            {
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({EngineeringPositionId}, {DemoOrganizationId}, {EngineeringDeptId}, 'Software Engineer', 'Entry-level software developer', 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({HrPositionId}, {DemoOrganizationId}, {HrDeptId}, 'HR Staff', 'Human Resources Officer', 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({ManagerPositionId}, {DemoOrganizationId}, {EngineeringDeptId}, 'Engineering Manager', 'Engineering Team Lead', 5, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({FinancePositionId}, {DemoOrganizationId}, {FinanceDeptId}, 'Finance Staff', 'Finance & Accounting Officer', 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({SalesPositionId}, {DemoOrganizationId}, {SalesDeptId}, 'Sales Executive', 'Sales & Marketing Officer', 1, TRUE, FALSE, {now}, {now})");
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Positions"" (""Id"", ""OrganizationId"", ""DepartmentId"", ""Title"", ""Description"", ""Grade"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({WarehousePositionId}, {DemoOrganizationId}, {WarehouseDeptId}, 'Warehouse Staff', 'Warehouse & Logistics Officer', 1, TRUE, FALSE, {now}, {now})");
            });
        }
    }

    // ============ EMPLOYEES (each with a linked login User) ============
    // Depends on Departments/Positions already existing - call
    // SeedDepartmentsAndPositionsAsync first if calling this standalone.
    public static async Task SeedEmployeesAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (await context.Employees.IgnoreQueryFilters().AnyAsync())
            return;

        logger.LogInformation("Seeding employees...");

        var employeePasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!");

        var employees = new (
            Guid UserId, Guid EmployeeId, string EmployeeNumber, string Username, string Email,
            string FirstName, string LastName, int Gender, int MaritalStatus,
            Guid DepartmentId, Guid PositionId, int EmploymentType,
            DateTime DateOfBirth, DateTime HireDate, decimal BasicSalary, string Phone)[]
        {
            (Guid.Parse("00000000-0000-0000-0000-000000000110"), Guid.Parse("00000000-0000-0000-0000-000000000120"), "EMP001",
             "budi.santoso", "budi.santoso@nexterp.com", "Budi", "Santoso", 1, 2,
             EngineeringDeptId, EngineeringPositionId, 1,
             new DateTime(1992, 3, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc), 12000000m, "+62812-3456-7001"),

            (Guid.Parse("00000000-0000-0000-0000-000000000111"), Guid.Parse("00000000-0000-0000-0000-000000000121"), "EMP002",
             "siti.rahayu", "siti.rahayu@nexterp.com", "Siti", "Rahayu", 2, 2,
             EngineeringDeptId, ManagerPositionId, 1,
             new DateTime(1986, 8, 22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 2, 15, 0, 0, 0, DateTimeKind.Utc), 25000000m, "+62813-4567-8002"),

            (Guid.Parse("00000000-0000-0000-0000-000000000112"), Guid.Parse("00000000-0000-0000-0000-000000000122"), "EMP003",
             "maya.anggraini", "maya.anggraini@nexterp.com", "Maya", "Anggraini", 2, 1,
             EngineeringDeptId, EngineeringPositionId, 4,
             new DateTime(1998, 11, 5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9, 2, 0, 0, 0, DateTimeKind.Utc), 9000000m, "+62814-5678-9003"),

            (Guid.Parse("00000000-0000-0000-0000-000000000113"), Guid.Parse("00000000-0000-0000-0000-000000000123"), "EMP004",
             "dewi.lestari", "dewi.lestari@nexterp.com", "Dewi", "Lestari", 2, 1,
             HrDeptId, HrPositionId, 1,
             new DateTime(1994, 1, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 4, 11, 0, 0, 0, DateTimeKind.Utc), 10000000m, "+62815-6789-0004"),

            (Guid.Parse("00000000-0000-0000-0000-000000000114"), Guid.Parse("00000000-0000-0000-0000-000000000124"), "EMP005",
             "ahmad.fauzi", "ahmad.fauzi@nexterp.com", "Ahmad", "Fauzi", 1, 2,
             FinanceDeptId, FinancePositionId, 1,
             new DateTime(1989, 5, 17, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 7, 20, 0, 0, 0, DateTimeKind.Utc), 11500000m, "+62816-7890-1005"),

            (Guid.Parse("00000000-0000-0000-0000-000000000115"), Guid.Parse("00000000-0000-0000-0000-000000000125"), "EMP006",
             "rina.wulandari", "rina.wulandari@nexterp.com", "Rina", "Wulandari", 2, 1,
             SalesDeptId, SalesPositionId, 1,
             new DateTime(1996, 9, 9, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 9, 0, 0, 0, DateTimeKind.Utc), 9500000m, "+62817-8901-2006"),

            (Guid.Parse("00000000-0000-0000-0000-000000000116"), Guid.Parse("00000000-0000-0000-0000-000000000126"), "EMP007",
             "eko.prasetyo", "eko.prasetyo@nexterp.com", "Eko", "Prasetyo", 1, 2,
             SalesDeptId, SalesPositionId, 1,
             new DateTime(1991, 12, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 10, 4, 0, 0, 0, DateTimeKind.Utc), 9500000m, "+62818-9012-3007"),

            (Guid.Parse("00000000-0000-0000-0000-000000000117"), Guid.Parse("00000000-0000-0000-0000-000000000127"), "EMP008",
             "agus.hermawan", "agus.hermawan@nexterp.com", "Agus", "Hermawan", 1, 2,
             WarehouseDeptId, WarehousePositionId, 1,
             new DateTime(1988, 7, 25, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 3, 16, 0, 0, 0, DateTimeKind.Utc), 8500000m, "+62819-0123-4008"),
        };

        // One transaction for the whole roster: a failure partway (e.g. a dropped
        // connection on employee #5) rolls back every employee inserted so far,
        // so the AnyAsync() guard above still sees an empty table and retries
        // the full roster on the next startup instead of leaving it half-seeded.
        await RunInTransactionAsync(context, async () =>
        {
            foreach (var e in employees)
            {
                // Each employee gets its own login (Employees "extends" Users with HR
                // data via the UserId FK) - no role assigned, so logging in as one
                // yields an authenticated but unprivileged account, same as a real
                // non-admin hire would have until granted a role.
                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Users"" (""Id"", ""OrganizationId"", ""Username"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""Phone"", ""IsActive"", ""IsSuperAdmin"", ""FailedLoginAttempts"", ""LockedUntil"", ""LastLoginAt"", ""LastLoginIp"", ""RefreshTokenHash"", ""RefreshTokenExpiry"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({e.UserId}, {DemoOrganizationId}, {e.Username}, {e.Email}, {employeePasswordHash}, {e.FirstName}, {e.LastName}, {e.Phone}, TRUE, FALSE, 0, NULL, NULL, NULL, NULL, NULL, FALSE, {now}, {now})");

                await context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""Employees"" (""Id"", ""OrganizationId"", ""EmployeeNumber"", ""UserId"", ""FirstName"", ""LastName"", ""DateOfBirth"", ""Gender"", ""MaritalStatus"", ""DepartmentId"", ""PositionId"", ""EmploymentType"", ""Status"", ""HireDate"", ""BasicSalary"", ""PersonalEmail"", ""Phone"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                    ({e.EmployeeId}, {DemoOrganizationId}, {e.EmployeeNumber}, {e.UserId}, {e.FirstName}, {e.LastName}, {e.DateOfBirth}, {e.Gender}, {e.MaritalStatus}, {e.DepartmentId}, {e.PositionId}, {e.EmploymentType}, 1, {e.HireDate}, {e.BasicSalary}, {e.Email}, {e.Phone}, FALSE, {now}, {now})");
            }
        });
    }

    public static async Task SeedWarehousesAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (await context.Warehouses.IgnoreQueryFilters().AnyAsync())
            return;

        logger.LogInformation("Seeding warehouses...");
        await RunInTransactionAsync(context, async () =>
        {
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Warehouses"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""Address"", ""City"", ""Country"", ""Phone"", ""Email"", ""IsActive"", ""IsDefault"", ""AllowsNegativeStock"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({MainWarehouseId}, {DemoOrganizationId}, 'Main Warehouse', 'WH001', 'Primary storage facility', 'Jl. Sudirman No. 1', 'Jakarta', 'Indonesia', '+6221-555-0001', 'warehouse@nexterp.com', TRUE, TRUE, FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Warehouses"" (""Id"", ""OrganizationId"", ""Name"", ""Code"", ""Description"", ""Address"", ""City"", ""Country"", ""Phone"", ""Email"", ""IsActive"", ""IsDefault"", ""AllowsNegativeStock"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({SecondaryWarehouseId}, {DemoOrganizationId}, 'Secondary Warehouse', 'WH002', 'Backup storage facility', 'Jl. Gatot Subroto No. 50', 'Surabaya', 'Indonesia', '+6231-555-0002', 'warehouse2@nexterp.com', TRUE, FALSE, TRUE, FALSE, {now}, {now})");
        });
    }

    public static async Task SeedCustomersAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (await context.Customers.IgnoreQueryFilters().AnyAsync())
            return;

        logger.LogInformation("Seeding customers...");
        await RunInTransactionAsync(context, async () =>
        {
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Customers"" (""Id"", ""OrganizationId"", ""CustomerCode"", ""CustomerName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'CUST001', 'PT Maju Bersama', 2, 'contact@majubersama.co.id', '+6221-888-0001', 0, TRUE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Customers"" (""Id"", ""OrganizationId"", ""CustomerCode"", ""CustomerName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'CUST002', 'CV Sejahtera Utama', 2, 'info@sejahtera.co.id', '+6221-888-0002', 0, TRUE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Customers"" (""Id"", ""OrganizationId"", ""CustomerCode"", ""CustomerName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'CUST003', 'Toko Elektronik Jaya', 1, 'jaya@electronics.com', '+6281-234-5678', 0, TRUE, FALSE, {now}, {now})");
        });
    }

    public static async Task SeedSuppliersAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (await context.Suppliers.IgnoreQueryFilters().AnyAsync())
            return;

        logger.LogInformation("Seeding suppliers...");
        await RunInTransactionAsync(context, async () =>
        {
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Suppliers"" (""Id"", ""OrganizationId"", ""SupplierCode"", ""SupplierName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'SUP001', 'PT Sumber Prima', 2, 'sales@sumberprima.co.id', '+6221-555-1001', 0, TRUE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Suppliers"" (""Id"", ""OrganizationId"", ""SupplierCode"", ""SupplierName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'SUP002', 'CV Elektronik Grosir', 2, 'order@elegrosir.com', '+6221-555-1002', 0, TRUE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""Suppliers"" (""Id"", ""OrganizationId"", ""SupplierCode"", ""SupplierName"", ""Type"", ""Email"", ""Phone"", ""OutstandingAmount"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'SUP003', 'Toko Parts Automotive', 1, 'parts@automotive.com', '+6281-333-4444', 0, TRUE, FALSE, {now}, {now})");
        });
    }

    public static async Task SeedOrganizationSettingsAsync(ERPDbContext context, ILogger logger, DateTime now)
    {
        if (await context.OrganizationSettings.IgnoreQueryFilters().AnyAsync())
            return;

        logger.LogInformation("Seeding default organization settings...");

        await RunInTransactionAsync(context, async () =>
        {
            // HR Settings (Indonesian labor law defaults)
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'HR.OVERTIME.MAX_DAILY_HOURS', '4', 'HR', FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'HR.OVERTIME.MAX_WEEKLY_HOURS', '18', 'HR', FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'HR.LEAVE.ANNUAL_DEFAULT_DAYS', '12', 'HR', FALSE, FALSE, {now}, {now})");

            // Accounting Settings
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'ACC.DEFAULT_TERM_DAYS', '30', 'ACCOUNTING', FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'ACC.DEFAULT_TAX_RATE', '11', 'ACCOUNTING', FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'ACC.DEFAULT_CURRENCY', 'IDR', 'ACCOUNTING', FALSE, FALSE, {now}, {now})");

            // General Settings
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'GENERAL.TIMEZONE', 'Asia/Jakarta', 'GENERAL', FALSE, FALSE, {now}, {now})");
            await context.Database.ExecuteSqlAsync($@"
                INSERT INTO ""OrganizationSettings"" (""Id"", ""OrganizationId"", ""SettingKey"", ""SettingValue"", ""Category"", ""IsEncrypted"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"") VALUES
                ({Guid.NewGuid()}, {DemoOrganizationId}, 'GENERAL.DATE_FORMAT', 'dd/MM/yyyy', 'GENERAL', FALSE, FALSE, {now}, {now})");
        });
    }
}
#pragma warning restore EF1002
