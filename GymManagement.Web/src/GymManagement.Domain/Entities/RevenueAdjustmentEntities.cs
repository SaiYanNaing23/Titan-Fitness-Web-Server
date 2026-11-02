using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class FinancialAdjustmentRequest : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public RevenueTransactionType TransactionType { get; set; }
    public int ReferenceId { get; set; } // MemberSubscriptionId or SalesOrderId
    public string ReferenceNumber { get; set; } = string.Empty; // e.g. SUB-00042 or INV-000109
    public string CustomerOrMemberName { get; set; } = string.Empty;

    public decimal OriginalAmount { get; set; }
    public decimal ProposedAmount { get; set; }

    public PaymentMethod OriginalPaymentMethod { get; set; }
    public PaymentMethod? ProposedPaymentMethod { get; set; }

    public AdjustmentActionType ActionType { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    public AdjustmentApprovalStatus Status { get; set; } = AdjustmentApprovalStatus.Pending;

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewNotes { get; set; }
}
