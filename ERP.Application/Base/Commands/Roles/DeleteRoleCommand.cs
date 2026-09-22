using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Commands.Roles;

public class DeleteRoleCommand : ICommand<bool>
{
    public Guid Id { get; set; }
    // Ignored by the handler, which derives the organization from the
    // authenticated user; kept only for backward API compatibility.
    public Guid OrganizationId { get; set; }
}

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteRoleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        // Organization is taken from the authenticated user's context — see
        // UpdateRoleCommandHandler for why the request body can't be trusted here.
        if (_currentUser.OrganizationId == null)
            return Result<bool>.Failure("User is not associated with an organization");

        var organizationId = _currentUser.OrganizationId.Value;

        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id &&
                                    r.OrganizationId == organizationId, cancellationToken);

        if (role == null)
            return Result<bool>.Failure("Role not found");

        if (role.IsSystemRole)
            return Result<bool>.Failure("Cannot delete system role");

        var hasUsers = await _context.UserRoles
            .AnyAsync(ur => ur.RoleId == request.Id && !ur.IsDeleted, cancellationToken);

        if (hasUsers)
            return Result<bool>.Failure("Cannot delete role that is assigned to users. Remove all user assignments first.");

        // Soft delete the role
        role.MarkAsDeleted();
        role.Deactivate();

        // Remove all role permissions
        foreach (var permission in role.Permissions.ToList())
        {
            permission.MarkAsDeleted();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
