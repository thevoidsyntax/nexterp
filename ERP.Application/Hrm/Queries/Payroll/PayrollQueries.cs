using ERP.Domain.Hrm.Enums;
using ERP.Application.Common.Behaviors;
using ERP.Application.Common.Models;
using ERP.Application.Hrm.DTOs;
using MediatR;

namespace ERP.Application.Hrm.Queries.Payroll;

/// <summary>
/// Get paginated payroll records.
/// </summary>
[RequiresPermission("hrm.payroll.view")]
public record GetPayrollListQuery(
    Guid OrganizationId,
    int? Year = null,
    int? Month = null,
    Guid? EmployeeId = null,
    PayrollStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedList<PayrollListItemDto>>;

/// <summary>
/// Get payroll by ID.
/// </summary>
[RequiresPermission("hrm.payroll.view")]
public record GetPayrollByIdQuery(
    Guid OrganizationId,
    Guid PayrollId
) : IRequest<PayrollDto?>;

/// <summary>
/// Get payroll summary for a period.
/// </summary>
[RequiresPermission("hrm.payroll.view")]
public record GetPayrollSummaryQuery(
    Guid OrganizationId,
    int Year,
    int Month
) : IRequest<PayrollSummaryDto?>;

/// <summary>
/// Get payslip for employee.
/// </summary>
[RequiresPermission("hrm.payroll.view")]
public record GetPayslipQuery(
    Guid OrganizationId,
    Guid PayrollId
) : IRequest<PayslipDto?>;

/// <summary>
/// Get employee's payroll history.
/// </summary>
[RequiresPermission("hrm.payroll.view")]
public record GetEmployeePayrollHistoryQuery(
    Guid OrganizationId,
    Guid EmployeeId,
    int Page = 1,
    int PageSize = 12
) : IRequest<PaginatedList<PayrollDto>>;

/// <summary>
/// Payroll list item DTO.
/// </summary>
public record PayrollListItemDto
{
    public Guid Id { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal NetSalary { get; init; }
    public PayrollStatus Status { get; init; }
    public DateTime? PaymentDate { get; init; }
}
