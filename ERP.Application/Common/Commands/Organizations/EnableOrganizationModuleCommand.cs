using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Modules;

namespace ERP.Application.Common.Commands.Organizations;

public class EnableOrganizationModuleCommand : ICommand<bool>
{
    public Guid OrganizationId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
}

public class EnableOrganizationModuleCommandHandler : IRequestHandler<EnableOrganizationModuleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EnableOrganizationModuleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(EnableOrganizationModuleCommand request, CancellationToken cancellationToken)
    {
        // The route takes OrganizationId as a path parameter and allows the "Admin" role
        // (not just "SuperAdmin"), so without this check any org's Admin could enable
        // modules for a completely different organization by changing the URL.
        if (!_currentUser.IsSuperAdmin && _currentUser.OrganizationId != request.OrganizationId)
            return Result<bool>.Failure("Not authorized to manage modules for this organization");

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken);

        if (organization == null)
            return Result<bool>.Failure("Organization not found");

        // Get the module definition
        var moduleConfig = ModuleConfigurationLoader.GetAllModules()
            .FirstOrDefault(m => m.Module.Equals(request.ModuleCode, StringComparison.OrdinalIgnoreCase));

        if (moduleConfig == null)
            return Result<bool>.Failure($"Module '{request.ModuleCode}' not found");

        // Check if module is already enabled
        var existingModule = await _context.OrganizationModules
            .FirstOrDefaultAsync(om => om.OrganizationId == request.OrganizationId &&
                                     om.ModuleCode == request.ModuleCode.ToUpperInvariant() &&
                                     !om.IsDeleted, cancellationToken);

        if (existingModule != null)
            return Result<bool>.Success(true); // Already enabled

        // Check license tier for the organization
        var license = await _context.OrganizationLicenses
            .Include(l => l.LicenseTier)
            .Where(l => l.OrganizationId == request.OrganizationId &&
                       !l.IsDeleted &&
                       l.EndDate >= DateTime.UtcNow)
            .OrderByDescending(l => l.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (license == null)
            return Result<bool>.Failure("No active license found for organization");

        // Check if module tier is accessible
        var requiredTier = moduleConfig.Tier;
        var userTier = license.LicenseTier?.Code ?? "STARTER";

        // Simple tier check - higher tier number means higher tier
        var tierOrder = new Dictionary<string, int>
        {
            { "STARTER", 1 },
            { "PROFESSIONAL", 2 },
            { "ENTERPRISE", 3 }
        };

        var userTierLevel = tierOrder.GetValueOrDefault(userTier.ToUpperInvariant(), 1);
        var requiredTierLevel = tierOrder.GetValueOrDefault(requiredTier.ToUpperInvariant(), 1);

        if (userTierLevel < requiredTierLevel)
            return Result<bool>.Failure($"Module '{request.ModuleCode}' requires {requiredTier} tier or higher");

        // Create the organization module
        var orgModule = new Domain.Common.Modules.OrganizationModule(
            request.OrganizationId,
            moduleConfig.Module,
            moduleConfig.Code);

        _context.OrganizationModules.Add(orgModule);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
