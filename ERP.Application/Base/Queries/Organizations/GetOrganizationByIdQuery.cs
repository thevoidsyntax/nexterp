using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Queries.Organizations;

public class GetOrganizationByIdQuery : IQuery<OrganizationDto>
{
    public Guid Id { get; set; }
}

public class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, Result<OrganizationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetOrganizationByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<OrganizationDto>> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        // Same non-SuperAdmin restriction as GetOrganizationsQuery - see there for why.
        if (!_currentUser.IsSuperAdmin && _currentUser.OrganizationId != request.Id)
            return Result<OrganizationDto>.Failure("Not authorized to view this organization");

        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted, cancellationToken);

        if (org == null)
            return Result<OrganizationDto>.Failure("Organization not found");

        var userCount = await _context.Users
            .CountAsync(u => u.OrganizationId == org.Id && !u.IsDeleted, cancellationToken);

        var moduleCount = await _context.OrganizationModules
            .CountAsync(m => m.OrganizationId == org.Id && !m.IsDeleted, cancellationToken);

        return Result<OrganizationDto>.Success(new OrganizationDto
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
}
