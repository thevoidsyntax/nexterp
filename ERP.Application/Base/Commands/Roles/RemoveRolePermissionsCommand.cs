using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Commands.Roles;

public class RemoveRolePermissionsCommand : ICommand<bool>
{
    public Guid RoleId { get; set; }
    // Ignored by the handler, which derives the organization from the
    // authenticated user; kept only for backward API compatibility.
    public Guid OrganizationId { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RemoveRolePermissionsCommandHandler : IRequestHandler<RemoveRolePermissionsCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveRolePermissionsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RemoveRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        // Organization is taken from the authenticated user's context — see
        // UpdateRoleCommandHandler for why the request body can't be trusted here.
        if (_currentUser.OrganizationId == null)
            return Result<bool>.Failure("User is not associated with an organization");

        var organizationId = _currentUser.OrganizationId.Value;

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId &&
                                    r.OrganizationId == organizationId, cancellationToken);

        if (role == null)
            return Result<bool>.Failure("Role not found");

        if (role.IsSystemRole)
            return Result<bool>.Failure("Cannot modify system role permissions");

        var rolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId &&
                        request.Permissions.Contains(rp.Permission) &&
                        !rp.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var permission in rolePermissions)
        {
            permission.MarkAsDeleted();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
