using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.DTOs;

namespace ERP.Application.Base.Queries.Organizations;

public class GetOrganizationsQuery : IQuery<PaginatedList<OrganizationDto>>
{
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
    public PaginationParams Pagination { get; set; } = new();
}

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public int UserCount { get; set; }
    public int ModuleCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, Result<PaginatedList<OrganizationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetOrganizationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<OrganizationDto>>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Organizations.Where(o => !o.IsDeleted).AsQueryable();

        // Organization is the tenant root, so there is no OrganizationId FK to scope
        // by the way Roles/Users are - identity is the row itself. A non-SuperAdmin
        // only ever sees their own organization.
        if (!_currentUser.IsSuperAdmin)
        {
            if (_currentUser.OrganizationId == null)
                return Result<PaginatedList<OrganizationDto>>.Failure("User is not associated with an organization");

            query = query.Where(o => o.Id == _currentUser.OrganizationId.Value);
        }

        if (request.IsActive.HasValue)
            query = query.Where(o => o.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(searchLower) ||
                                   (o.Code != null && o.Code.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var organizations = await query
            .OrderBy(o => o.Name)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = new List<OrganizationDto>();

        foreach (var org in organizations)
        {
            var userCount = await _context.Users
                .CountAsync(u => u.OrganizationId == org.Id && !u.IsDeleted, cancellationToken);

            var moduleCount = await _context.OrganizationModules
                .CountAsync(m => m.OrganizationId == org.Id && !m.IsDeleted, cancellationToken);

            dtos.Add(new OrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Code = org.Code,
                TaxId = org.TaxId,
                Phone = org.Phone,
                Email = org.Email,
                Address = org.Address,
                City = org.City,
                Country = org.Country,
                PostalCode = org.PostalCode,
                IsActive = org.IsActive,
                LicenseExpiry = org.LicenseExpiry,
                UserCount = userCount,
                ModuleCount = moduleCount,
                CreatedAt = org.CreatedAt,
                UpdatedAt = org.UpdatedAt
            });
        }

        return Result<PaginatedList<OrganizationDto>>.Success(new PaginatedList<OrganizationDto>(
            dtos,
            totalCount,
            request.Pagination.Page,
            request.Pagination.PageSize
        ));
    }
}
