using MediatR;
using ERP.Application.Common.Base;
using ERP.Application.Common.Behaviors;

namespace ERP.Application.Accounting.Commands.Accounts;

/// <summary>
/// Command to delete an account (soft delete)
/// </summary>
[RequiresPermission("accounting.accounts.delete")]
public class DeleteAccountCommand : ICommand<bool>
{
    public Guid Id { get; set; }
}
