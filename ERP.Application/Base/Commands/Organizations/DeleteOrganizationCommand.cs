using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Commands.Organizations;

public class DeleteOrganizationCommand : ICommand<bool>
{
    public Guid Id { get; set; }
}

public class DeleteOrganizationCommandHandler : IRequestHandler<DeleteOrganizationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteOrganizationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteOrganizationCommand request, CancellationToken cancellationToken)
    {
        // Deleting an organization deletes a tenant, not a child record within one -
        // restrict to SuperAdmin only, unlike Roles which an org's own Admin can delete.
        if (!_currentUser.IsSuperAdmin)
            return Result<bool>.Failure("Only a super administrator can delete organizations");

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted, cancellationToken);

        if (organization == null)
            return Result<bool>.Failure("Organization not found");

        var hasUsers = await _context.Users
            .AnyAsync(u => u.OrganizationId == request.Id && !u.IsDeleted, cancellationToken);

        if (hasUsers)
            return Result<bool>.Failure("Cannot delete an organization that still has users. Remove or reassign all users first.");

        organization.MarkAsDeleted();
        organization.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
