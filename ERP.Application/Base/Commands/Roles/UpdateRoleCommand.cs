using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Base;

namespace ERP.Application.Base.Commands.Roles;

public class UpdateRoleCommand : ICommand<bool>
{
    public Guid Id { get; set; }
    // Ignored by the handler, which derives the organization from the
    // authenticated user; kept only for backward API compatibility.
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required")
            .MaximumLength(100).WithMessage("Role name cannot exceed 100 characters");
    }
}

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateRoleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        // Organization is taken from the authenticated user's context, not the request
        // body — Role isn't an ITenantEntity, so the global tenant filter doesn't scope
        // this query, and a client-supplied OrganizationId would let a caller reach into
        // another org's roles.
        if (_currentUser.OrganizationId == null)
            return Result<bool>.Failure("User is not associated with an organization");

        var organizationId = _currentUser.OrganizationId.Value;

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.Id &&
                                    r.OrganizationId == organizationId, cancellationToken);

        if (role == null)
            return Result<bool>.Failure("Role not found");

        if (role.IsSystemRole)
            return Result<bool>.Failure("Cannot modify system role");

        var existingRole = await _context.Roles
            .AnyAsync(r => r.OrganizationId == organizationId &&
                          r.Name.ToLower() == request.Name.ToLower() &&
                          r.Id != request.Id, cancellationToken);

        if (existingRole)
            return Result<bool>.Failure("Role with this name already exists");

        role.Update(request.Name, request.Description);

        if (request.IsActive)
            role.Activate();
        else
            role.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
