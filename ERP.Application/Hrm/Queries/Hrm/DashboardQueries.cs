using ERP.Application.Common.Behaviors;
using ERP.Application.Hrm.DTOs;
using MediatR;

namespace ERP.Application.Hrm.Queries.Hrm;

/// <summary>
/// Get HRM Dashboard data.
/// </summary>
[RequiresPermission("hrm.dashboard.read")]
public record GetHrmDashboardQuery(
    Guid OrganizationId,
    int? Year = null,
    int? Month = null
) : IRequest<HrmDashboardDto>;

/// <summary>
/// Get daily attendance report.
/// </summary>
[RequiresPermission("hrm.attendance.read")]
public record GetDailyAttendanceReportQuery(
    Guid OrganizationId,
    DateTime Date
) : IRequest<DailyAttendanceReportDto>;

/// <summary>
/// Get department statistics.
/// </summary>
[RequiresPermission("hrm.departments.read")]
public record GetDepartmentStatsQuery(
    Guid OrganizationId,
    Guid? DepartmentId = null
) : IRequest<List<DepartmentStatsDto>>;

/// <summary>
/// Get employee overview by department.
/// </summary>
[RequiresPermission("hrm.employees.read")]
public record GetEmployeeOverviewQuery(
    Guid OrganizationId,
    Guid? DepartmentId = null
) : IRequest<EmployeeOverviewDto>;

/// <summary>
/// Get attendance summary for period.
/// </summary>
[RequiresPermission("hrm.attendance.read")]
public record GetAttendanceSummaryQuery(
    Guid OrganizationId,
    DateTime StartDate,
    DateTime EndDate
) : IRequest<AttendanceOverviewDto>;
