using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Base;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Base;

namespace ERP.Application.Base.Commands.Users;

/// <summary>
/// Handler for CreateUserCommand
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateUserCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Organization is taken from the authenticated user's context, not the
        // request body, so a caller cannot create users under another org.
        if (_currentUser.OrganizationId == null)
            return Result<Guid>.Failure("User is not associated with an organization");

        var organizationId = _currentUser.OrganizationId.Value;

        var organizationExists = await _context.Organizations
            .AnyAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (!organizationExists)
            return Result<Guid>.Failure("Organization not found");

        // Check if username already exists
        var existingUsername = await _context.Users
            .AnyAsync(u => u.Username == request.Username.ToLowerInvariant(), cancellationToken);

        if (existingUsername)
            return Result<Guid>.Failure("Username already exists");

        // Check if email already exists
        var existingEmail = await _context.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (existingEmail)
            return Result<Guid>.Failure("Email already exists");

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = User.Create(
            organizationId,
            request.Username,
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            request.Phone);

        _context.Users.Add(user);

        // Assign roles if provided. Role isn't an ITenantEntity, so the global
        // tenant filter doesn't scope this query — filter explicitly here to
        // stop a caller from assigning a role that belongs to another org.
        if (request.RoleIds != null && request.RoleIds.Any())
        {
            var validRoles = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && r.OrganizationId == organizationId && !r.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var role in validRoles)
            {
                user.AssignRole(UserRole.Create(user.Id, role.Id));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(user.Id);
    }
}
