using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// OrganizationModule.ModuleCode was added to the domain entity in a prior commit
    /// without a matching migration, so this column never existed in any migrated
    /// database even though the entity, EnableOrganizationModuleCommand, and
    /// GetOrganizationModulesQuery all already depend on it. Not related to Money —
    /// this drift surfaced only because generating a migration for the Money value
    /// object (which needs none, since it maps to the same decimal column) forced EF
    /// to reconcile the model snapshot against this pre-existing gap first.
    /// </summary>
    public partial class AddOrganizationModuleModuleCodeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModuleCode",
                table: "OrganizationModules",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModuleCode",
                table: "OrganizationModules");
        }
    }
}
