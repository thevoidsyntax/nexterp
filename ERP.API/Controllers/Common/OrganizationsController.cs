using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

using ERP.API.Controllers.Base;
using ERP.Application.Base.Commands.Organizations;
using ERP.Application.Base.Queries.Organizations;
using ERP.Application.Common.DTOs;

namespace ERP.API.Controllers.Common;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v1/organizations")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class OrganizationsController : BaseApiController
{
    private readonly IMediator _mediator;

    public OrganizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all organizations with pagination. A SuperAdmin sees every organization;
    /// any other caller only ever sees their own.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrganizations(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrganizationsQuery
        {
            IsActive = isActive,
            Search = search,
            Pagination = new PaginationParams { Page = page, PageSize = pageSize }
        };

        var result = await _mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Get organization by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrganizationByIdQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new organization. SuperAdmin only - see CreateOrganizationCommandHandler.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOrganizationCommand
        {
            Name = request.Name,
            Code = request.Code,
            TaxId = request.TaxId,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            PostalCode = request.PostalCode,
            LicenseExpiry = request.LicenseExpiry
        };

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsSuccess)
            return Created($"api/v1/organizations/{result.Value}", result);

        return HandleResult(result);
    }

    /// <summary>
    /// Update an existing organization
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrganizationCommand
        {
            Id = id,
            Name = request.Name,
            Code = request.Code,
            TaxId = request.TaxId,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            PostalCode = request.PostalCode,
            LicenseExpiry = request.LicenseExpiry,
            IsActive = request.IsActive
        };

        var result = await _mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete an organization. SuperAdmin only - see DeleteOrganizationCommandHandler.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteOrganizationCommand { Id = id };
        var result = await _mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

#region DTOs

public class CreateOrganizationRequest
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

public class UpdateOrganizationRequest
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
    public bool IsActive { get; set; } = true;
}

#endregion
