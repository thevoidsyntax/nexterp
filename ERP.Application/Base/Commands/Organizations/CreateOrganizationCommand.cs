using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Base;

namespace ERP.Application.Base.Commands.Organizations;

public class CreateOrganizationCommand : ICommand<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateTime? LicenseExpiry { get; set; }
}

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required")
            .MaximumLength(200).WithMessage("Organization name cannot exceed 200 characters");

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Organization code cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateOrganizationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        // Organization is the tenant root, so provisioning a new one is a platform-level
        // action, not something an org's own Admin can do for itself (unlike Roles/Users).
        if (!_currentUser.IsSuperAdmin)
            return Result<Guid>.Failure("Only a super administrator can create organizations");

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var codeExists = await _context.Organizations
                .AnyAsync(o => !o.IsDeleted && o.Code != null && o.Code.ToLower() == request.Code.ToLower(), cancellationToken);

            if (codeExists)
                return Result<Guid>.Failure("An organization with this code already exists");
        }

        var organization = Organization.Create(
            request.Name, request.Code, request.TaxId, request.Phone, request.Email,
            request.Address, request.City, request.Country, request.PostalCode, request.LicenseExpiry);

        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(organization.Id);
    }
}
