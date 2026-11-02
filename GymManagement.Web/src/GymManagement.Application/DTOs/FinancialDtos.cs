using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs;

public class DailyRevenueSummaryDto
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal MemberFeeTotal { get; set; }
    public int MemberFeeCount { get; set; }
    public decimal StockSalesTotal { get; set; }
    public int StockSalesCount { get; set; }
    public int TotalTransactionsCount { get; set; }
    public int PendingAdjustmentsCount { get; set; }
    public List<BranchRevenueSummaryDto> BranchSummaries { get; set; } = new();
}

public class BranchRevenueSummaryDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public decimal MemberFeeRevenue { get; set; }
    public decimal StockSalesRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class RevenueTransactionDto
{
    public int Id { get; set; }
    public RevenueTransactionType TransactionType { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string CustomerOrMemberName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string CashierOrStaff { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountApplied { get; set; }

    public bool IsLocked { get; set; } = true;
    public bool IsAdjustmentPending { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }

    public int? PendingAdjustmentId { get; set; }
    public AdjustmentActionType? PendingAdjustmentAction { get; set; }
    public string? PendingAdjustmentReason { get; set; }
}

public class FinancialAdjustmentRequestDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public RevenueTransactionType TransactionType { get; set; }
    public int ReferenceId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CustomerOrMemberName { get; set; } = string.Empty;

    public decimal OriginalAmount { get; set; }
    public decimal ProposedAmount { get; set; }

    public PaymentMethod OriginalPaymentMethod { get; set; }
    public PaymentMethod? ProposedPaymentMethod { get; set; }

    public AdjustmentActionType ActionType { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }

    public AdjustmentApprovalStatus Status { get; set; }

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewNotes { get; set; }
}
