using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Base.Commands.Organizations;

public class UpdateOrganizationCommand : ICommand<bool>
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
    public DateTime? LicenseExpiry { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Organization ID is required");

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

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateOrganizationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        // A SuperAdmin may update any organization; a regular Admin may only update
        // their own. Organization is the tenant root itself, so "own org" here means
        // the current user's OrganizationId, not a client-supplied value.
        if (!_currentUser.IsSuperAdmin && _currentUser.OrganizationId != request.Id)
            return Result<bool>.Failure("Not authorized to update this organization");

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted, cancellationToken);

        if (organization == null)
            return Result<bool>.Failure("Organization not found");

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var codeExists = await _context.Organizations
                .AnyAsync(o => !o.IsDeleted && o.Id != request.Id &&
                              o.Code != null && o.Code.ToLower() == request.Code.ToLower(), cancellationToken);

            if (codeExists)
                return Result<bool>.Failure("An organization with this code already exists");
        }

        organization.Update(
            request.Name, request.Code, request.TaxId, request.Phone, request.Email,
            request.Address, request.City, request.Country, request.PostalCode, request.LicenseExpiry);

        if (request.IsActive)
            organization.Activate();
        else
            organization.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
