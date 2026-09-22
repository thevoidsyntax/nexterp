using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.DTOs;
using ERP.Application.Common.Interfaces;
using ERP.Application.Base.DTOs;

namespace ERP.Application.Base.Queries.Users;

/// <summary>
/// Query to get user by ID
/// </summary>
public class GetUserByIdQuery : IQuery<UserDto>
{
    public Guid Id { get; set; }
}

/// <summary>
/// Query to get paginated users
/// </summary>
public class GetUsersPaginatedQuery : IQuery<PaginatedResult<UserDto>>
{
    public Guid? OrganizationId { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
    public PaginationParams Pagination { get; set; } = new();
}

/// <summary>
/// Handler for GetUserByIdQuery
/// </summary>
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        // User isn't covered by the global tenant filter (see ERPDbContext), so without
        // this check any authenticated caller could fetch any other org's user by GUID.
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);

        if (user == null)
            return Result<UserDto>.Failure("User not found");

        if (!_currentUser.IsSuperAdmin && user.OrganizationId != _currentUser.OrganizationId)
            return Result<UserDto>.Failure("User not found");

        var dto = new UserDto
        {
            Id = user.Id,
            OrganizationId = user.OrganizationId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            IsActive = user.IsActive,
            IsSuperAdmin = user.IsSuperAdmin,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = new List<string>()
        };

        return Result<UserDto>.Success(dto);
    }
}

/// <summary>
/// Handler for GetUsersPaginatedQuery
/// </summary>
public class GetUsersPaginatedQueryHandler : IRequestHandler<GetUsersPaginatedQuery, Result<PaginatedResult<UserDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUsersPaginatedQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<UserDto>>> Handle(GetUsersPaginatedQuery request, CancellationToken cancellationToken)
    {
        // Only a SuperAdmin may list users across organizations (or filter by an
        // arbitrary OrganizationId); everyone else is always scoped to their own org,
        // regardless of what was requested — User isn't covered by the global tenant
        // filter (see ERPDbContext), so an unfiltered request would otherwise return
        // every organization's users. Fail closed (not "list everything") if a
        // non-SuperAdmin somehow has no OrganizationId.
        if (!_currentUser.IsSuperAdmin && _currentUser.OrganizationId == null)
            return Result<PaginatedResult<UserDto>>.Failure("User is not associated with an organization");

        var organizationFilter = _currentUser.IsSuperAdmin ? request.OrganizationId : _currentUser.OrganizationId;

        var query = _context.Users.AsNoTracking().Where(u => !u.IsDeleted);

        if (organizationFilter.HasValue)
            query = query.Where(u => u.OrganizationId == organizationFilter.Value);

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search) ||
                u.FirstName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Username)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(user => new UserDto
        {
            Id = user.Id,
            OrganizationId = user.OrganizationId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            IsActive = user.IsActive,
            IsSuperAdmin = user.IsSuperAdmin,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = new List<string>()
        }).ToList();

        var result = new PaginatedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Pagination.Page,
            PageSize = request.Pagination.PageSize
        };

        return Result<PaginatedResult<UserDto>>.Success(result);
    }
}
