using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Queries.Roles;

public class GetRoleByIdQuery : IQuery<RoleDetailDto>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
}

public class RoleDetailDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemRole { get; set; }
    public List<RolePermissionDto> Permissions { get; set; } = new();
    public List<RoleUserDto> Users { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RolePermissionDto
{
    public Guid Id { get; set; }
    public string Permission { get; set; } = string.Empty;
}

public class RoleUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, Result<RoleDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRoleByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<RoleDetailDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        // See GetRolesQueryHandler: Role isn't tenant-filtered, so this can't trust a
        // client-supplied OrganizationId except for a SuperAdmin.
        var organizationId = _currentUser.IsSuperAdmin ? request.OrganizationId : _currentUser.OrganizationId;

        var role = await _context.Roles
            .Include(r => r.Permissions.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == request.Id &&
                                    r.OrganizationId == organizationId &&
                                    !r.IsDeleted, cancellationToken);

        if (role == null)
            return Result<RoleDetailDto>.Failure("Role not found");

        var userIds = await _context.UserRoles
            .Where(ur => ur.RoleId == request.Id && !ur.IsDeleted)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new RoleUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FirstName + (u.LastName != null ? " " + u.LastName : ""),
                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken);

        var dto = new RoleDetailDto
        {
            Id = role.Id,
            OrganizationId = role.OrganizationId,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            IsSystemRole = role.IsSystemRole,
            Permissions = role.Permissions.Select(p => new RolePermissionDto
            {
                Id = p.Id,
                Permission = p.Permission
            }).ToList(),
            Users = users,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };

        return Result<RoleDetailDto>.Success(dto);
    }
}
