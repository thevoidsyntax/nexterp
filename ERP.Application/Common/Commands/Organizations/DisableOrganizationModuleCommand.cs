using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Common.Commands.Organizations;

public class DisableOrganizationModuleCommand : ICommand<bool>
{
    public Guid OrganizationId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
}

public class DisableOrganizationModuleCommandHandler : IRequestHandler<DisableOrganizationModuleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DisableOrganizationModuleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DisableOrganizationModuleCommand request, CancellationToken cancellationToken)
    {
        // See EnableOrganizationModuleCommandHandler: the route's OrganizationId path
        // parameter can't be trusted without tying it back to the caller.
        if (!_currentUser.IsSuperAdmin && _currentUser.OrganizationId != request.OrganizationId)
            return Result<bool>.Failure("Not authorized to manage modules for this organization");

        var orgModule = await _context.OrganizationModules
            .FirstOrDefaultAsync(om => om.OrganizationId == request.OrganizationId &&
                                 om.ModuleCode == request.ModuleCode.ToUpperInvariant() &&
                                 !om.IsDeleted, cancellationToken);

        if (orgModule == null)
            return Result<bool>.Failure("Module not enabled for organization");

        orgModule.MarkAsDeleted();
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
