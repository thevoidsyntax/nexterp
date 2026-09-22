using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Base;

namespace ERP.Application.Base.Commands.Users;

public class AssignUserRolesCommand : ICommand<bool>
{
    public Guid UserId { get; set; }
    // Ignored by the handler, which derives the organization from the
    // authenticated user; kept only for backward API compatibility.
    public Guid OrganizationId { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
}

public class AssignUserRolesCommandHandler : IRequestHandler<AssignUserRolesCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssignUserRolesCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(AssignUserRolesCommand request, CancellationToken cancellationToken)
    {
        // Organization is taken from the authenticated user's context, not the
        // request body — a client-supplied OrganizationId here would let a
        // caller reach into another org's users/roles.
        if (_currentUser.OrganizationId == null)
            return Result<bool>.Failure("User is not associated with an organization");

        var organizationId = _currentUser.OrganizationId.Value;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId &&
                                    u.OrganizationId == organizationId &&
                                    !u.IsDeleted, cancellationToken);

        if (user == null)
            return Result<bool>.Failure("User not found");

        // Get current role assignments
        var currentAssignments = await _context.UserRoles
            .Where(ur => ur.UserId == request.UserId && !ur.IsDeleted)
            .ToListAsync(cancellationToken);

        // Remove roles that are no longer in the list
        var rolesToRemove = currentAssignments
            .Where(ur => !request.RoleIds.Contains(ur.RoleId))
            .ToList();

        foreach (var assignment in rolesToRemove)
        {
            assignment.MarkAsDeleted();
        }

        // Add new role assignments
        var existingRoleIds = currentAssignments.Select(ur => ur.RoleId).ToList();
        var rolesToAdd = request.RoleIds.Where(id => !existingRoleIds.Contains(id)).ToList();

        foreach (var roleId in rolesToAdd)
        {
            var roleExists = await _context.Roles
                .AnyAsync(r => r.Id == roleId &&
                              r.OrganizationId == organizationId &&
                              !r.IsDeleted && r.IsActive, cancellationToken);

            if (roleExists)
            {
                var newAssignment = UserRole.Create(request.UserId, roleId);
                _context.UserRoles.Add(newAssignment);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
