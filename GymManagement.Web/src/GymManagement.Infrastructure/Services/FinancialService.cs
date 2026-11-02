using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class FinancialService : IFinancialService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public FinancialService(GymDbContext db, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<DailyRevenueSummaryDto> GetDailyRevenueSummaryAsync(DateTime date, int? branchId = null)
    {
        int tenantId = _currentUser.TenantId ?? 1;
        var start = date.Date;
        var end = start.AddDays(1);

        // 1. Query Member Subscriptions for the date
        var subQuery = _db.MemberSubscriptions
            .Include(s => s.Branch)
            .Include(s => s.Member)
            .Include(s => s.MembershipPlan)
            .Where(s => s.TenantId == tenantId && !s.IsVoided &&
                       ((s.CreatedAt >= start && s.CreatedAt < end) || (s.StartDate >= start && s.StartDate < end)));

        if (branchId.HasValue && branchId.Value > 0)
        {
            subQuery = subQuery.Where(s => s.BranchId == branchId.Value);
        }

        var subs = await subQuery.ToListAsync();

        // 2. Query Sales Orders (POS) for the date
        var salesQuery = _db.SalesOrders
            .Include(s => s.Branch)
            .Include(s => s.Member)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => s.TenantId == tenantId && !s.IsVoided &&
                        s.CreatedAt >= start && s.CreatedAt < end);

        if (branchId.HasValue && branchId.Value > 0)
        {
            salesQuery = salesQuery.Where(s => s.BranchId == branchId.Value);
        }

        var sales = await salesQuery.ToListAsync();

        // 3. Totals
        decimal memberFeeTotal = subs.Sum(s => s.FinalAmountPaid);
        int memberFeeCount = subs.Count;

        decimal stockSalesTotal = sales.Sum(s => s.TotalAmount);
        int stockSalesCount = sales.Count;

        decimal totalRevenue = memberFeeTotal + stockSalesTotal;

        // 4. Pending Adjustment Approvals Count
        var pendingReqCount = await _db.FinancialAdjustmentRequests
            .CountAsync(r => r.TenantId == tenantId && r.Status == AdjustmentApprovalStatus.Pending);

        // 5. Branch Breakdown
        var branches = await _db.Branches
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .ToListAsync();

        var branchSummaries = new List<BranchRevenueSummaryDto>();

        foreach (var b in branches)
        {
            var branchSubs = subs.Where(s => s.BranchId == b.Id).ToList();
            var branchSales = sales.Where(s => s.BranchId == b.Id).ToList();

            decimal bSubRev = branchSubs.Sum(s => s.FinalAmountPaid);
            decimal bSalesRev = branchSales.Sum(s => s.TotalAmount);
            decimal bTotal = bSubRev + bSalesRev;

            branchSummaries.Add(new BranchRevenueSummaryDto
            {
                BranchId = b.Id,
                BranchName = b.BranchName,
                BranchCode = b.BranchCode,
                MemberFeeRevenue = bSubRev,
                StockSalesRevenue = bSalesRev,
                TotalRevenue = bTotal,
                TransactionCount = branchSubs.Count + branchSales.Count,
                PercentageOfTotal = totalRevenue > 0 ? Math.Round((bTotal / totalRevenue) * 100m, 1) : 0
            });
        }

        return new DailyRevenueSummaryDto
        {
            Date = start,
            TotalRevenue = totalRevenue,
            MemberFeeTotal = memberFeeTotal,
            MemberFeeCount = memberFeeCount,
            StockSalesTotal = stockSalesTotal,
            StockSalesCount = stockSalesCount,
            TotalTransactionsCount = memberFeeCount + stockSalesCount,
            PendingAdjustmentsCount = pendingReqCount,
            BranchSummaries = branchSummaries.OrderByDescending(b => b.TotalRevenue).ToList()
        };
    }

    public async Task<List<RevenueTransactionDto>> GetRevenueTransactionsAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int? branchId = null, 
        RevenueTransactionType? type = null, 
        PaymentMethod? paymentMethod = null, 
        string? search = null)
    {
        int tenantId = _currentUser.TenantId ?? 1;
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1);

        var result = new List<RevenueTransactionDto>();

        // Fetch pending adjustment requests to map status
        var pendingRequests = await _db.FinancialAdjustmentRequests
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        // 1. Member Subscriptions
        if (!type.HasValue || type == RevenueTransactionType.MemberFee)
        {
            var subQuery = _db.MemberSubscriptions
                .Include(s => s.Branch)
                .Include(s => s.Member)
                .Include(s => s.MembershipPlan)
                .Where(s => s.TenantId == tenantId &&
                           ((s.CreatedAt >= start && s.CreatedAt < end) || (s.StartDate >= start && s.StartDate < end)));

            if (branchId.HasValue && branchId.Value > 0)
                subQuery = subQuery.Where(s => s.BranchId == branchId.Value);

            if (paymentMethod.HasValue)
                subQuery = subQuery.Where(s => s.PaymentMethod == paymentMethod.Value);

            var subs = await subQuery.ToListAsync();

            foreach (var s in subs)
            {
                var refNum = $"SUB-{s.Id:D5}";
                var adj = pendingRequests.FirstOrDefault(r => r.TransactionType == RevenueTransactionType.MemberFee && r.ReferenceId == s.Id && r.Status == AdjustmentApprovalStatus.Pending);

                result.Add(new RevenueTransactionDto
                {
                    Id = s.Id,
                    TransactionType = RevenueTransactionType.MemberFee,
                    ReferenceNumber = refNum,
                    TransactionDate = s.CreatedAt != default ? s.CreatedAt : s.StartDate,
                    BranchId = s.BranchId,
                    BranchName = s.Branch?.BranchName ?? "Central HQ",
                    CustomerOrMemberName = s.Member?.FullName ?? "Member",
                    CustomerPhone = s.Member?.PhoneNumber,
                    ItemDescription = s.MembershipPlan?.PlanName ?? "Membership Plan",
                    CashierOrStaff = "Membership Front Desk",
                    PaymentMethod = s.PaymentMethod,
                    Amount = s.FinalAmountPaid,
                    OriginalPrice = s.OriginalPrice,
                    DiscountApplied = s.DiscountApplied,
                    IsLocked = true,
                    IsAdjustmentPending = s.IsAdjustmentPending || adj != null,
                    IsVoided = s.IsVoided,
                    VoidReason = s.VoidReason,
                    PendingAdjustmentId = adj?.Id,
                    PendingAdjustmentAction = adj?.ActionType,
                    PendingAdjustmentReason = adj?.Reason
                });
            }
        }

        // 2. POS Sales Orders
        if (!type.HasValue || type == RevenueTransactionType.StockSale)
        {
            var salesQuery = _db.SalesOrders
                .Include(s => s.Branch)
                .Include(s => s.Member)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .Where(s => s.TenantId == tenantId &&
                            s.CreatedAt >= start && s.CreatedAt < end);

            if (branchId.HasValue && branchId.Value > 0)
                salesQuery = salesQuery.Where(s => s.BranchId == branchId.Value);

            if (paymentMethod.HasValue)
                salesQuery = salesQuery.Where(s => s.PaymentMethod == paymentMethod.Value);

            var sales = await salesQuery.ToListAsync();

            foreach (var o in sales)
            {
                var adj = pendingRequests.FirstOrDefault(r => r.TransactionType == RevenueTransactionType.StockSale && r.ReferenceId == o.Id && r.Status == AdjustmentApprovalStatus.Pending);
                var itemsSummary = string.Join(", ", o.Items.Select(i => $"{i.Product?.ProductName ?? "Item"} x{i.Quantity}"));

                result.Add(new RevenueTransactionDto
                {
                    Id = o.Id,
                    TransactionType = RevenueTransactionType.StockSale,
                    ReferenceNumber = o.InvoiceNumber,
                    TransactionDate = o.CreatedAt,
                    BranchId = o.BranchId,
                    BranchName = o.Branch?.BranchName ?? "Branch POS",
                    CustomerOrMemberName = o.Member?.FullName ?? "Walk-in Guest",
                    CustomerPhone = o.Member?.PhoneNumber,
                    ItemDescription = string.IsNullOrWhiteSpace(itemsSummary) ? "POS Retail Merchandise" : itemsSummary,
                    CashierOrStaff = string.IsNullOrWhiteSpace(o.CashierName) ? "Branch Cashier" : o.CashierName,
                    PaymentMethod = o.PaymentMethod,
                    Amount = o.TotalAmount,
                    OriginalPrice = o.SubTotal,
                    DiscountApplied = o.DiscountAmount,
                    IsLocked = true,
                    IsAdjustmentPending = o.IsAdjustmentPending || adj != null,
                    IsVoided = o.IsVoided,
                    VoidReason = o.VoidReason,
                    PendingAdjustmentId = adj?.Id,
                    PendingAdjustmentAction = adj?.ActionType,
                    PendingAdjustmentReason = adj?.Reason
                });
            }
        }

        // Apply Search query
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            result = result.Where(r => 
                r.ReferenceNumber.ToLowerInvariant().Contains(s) ||
                r.CustomerOrMemberName.ToLowerInvariant().Contains(s) ||
                (r.CustomerPhone != null && r.CustomerPhone.Contains(s)) ||
                r.ItemDescription.ToLowerInvariant().Contains(s) ||
                r.CashierOrStaff.ToLowerInvariant().Contains(s) ||
                r.BranchName.ToLowerInvariant().Contains(s)
            ).ToList();
        }

        return result.OrderBy(r => r.TransactionDate).ToList();
    }

    public async Task<FinancialAdjustmentRequestDto> SubmitAdjustmentRequestAsync(
        RevenueTransactionType type, 
        int referenceId, 
        AdjustmentActionType actionType, 
        decimal proposedAmount, 
        PaymentMethod? proposedPaymentMethod, 
        string reason, 
        string requestedBy)
    {
        int tenantId = _currentUser.TenantId ?? 1;
        int? branchId = _currentUser.BranchId;
        string refNumber = string.Empty;
        string customerName = string.Empty;
        decimal originalAmount = 0;
        PaymentMethod originalMethod = PaymentMethod.Cash;

        if (type == RevenueTransactionType.MemberFee)
        {
            var sub = await _db.MemberSubscriptions
                .Include(s => s.Member)
                .FirstOrDefaultAsync(s => s.Id == referenceId);

            if (sub == null) throw new InvalidOperationException("Member subscription record not found.");

            branchId ??= sub.BranchId;
            refNumber = $"SUB-{sub.Id:D5}";
            customerName = sub.Member?.FullName ?? "Member";
            originalAmount = sub.FinalAmountPaid;
            originalMethod = sub.PaymentMethod;

            sub.IsAdjustmentPending = true;
        }
        else
        {
            var order = await _db.SalesOrders
                .Include(o => o.Member)
                .FirstOrDefaultAsync(o => o.Id == referenceId);

            if (order == null) throw new InvalidOperationException("Sales order record not found.");

            branchId ??= order.BranchId;
            refNumber = order.InvoiceNumber;
            customerName = order.Member?.FullName ?? "Walk-in Guest";
            originalAmount = order.TotalAmount;
            originalMethod = order.PaymentMethod;

            order.IsAdjustmentPending = true;
        }

        var request = new FinancialAdjustmentRequest
        {
            TenantId = tenantId,
            BranchId = branchId,
            TransactionType = type,
            ReferenceId = referenceId,
            ReferenceNumber = refNumber,
            CustomerOrMemberName = customerName,
            OriginalAmount = originalAmount,
            ProposedAmount = actionType == AdjustmentActionType.VoidRefund ? 0 : proposedAmount,
            OriginalPaymentMethod = originalMethod,
            ProposedPaymentMethod = proposedPaymentMethod,
            ActionType = actionType,
            Reason = reason,
            RequestedBy = requestedBy,
            RequestedDate = DateTime.UtcNow,
            Status = AdjustmentApprovalStatus.Pending
        };

        _db.FinancialAdjustmentRequests.Add(request);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: tenantId,
            branchId: branchId,
            userId: _currentUser.UserId,
            username: requestedBy,
            userRole: _currentUser.Role?.ToString() ?? "Staff",
            actionType: "ADJUSTMENT_REQUESTED",
            module: "Financial",
            entityName: type.ToString(),
            entityId: refNumber,
            details: $"Staff '{requestedBy}' requested {actionType} for {type} {refNumber} (Original: {originalAmount:N0} MMK). Reason: {reason}",
            newValues: $"Action: {actionType}, Proposed: {request.ProposedAmount:N0} MMK"
        );

        return new FinancialAdjustmentRequestDto
        {
            Id = request.Id,
            TenantId = request.TenantId,
            BranchId = request.BranchId,
            TransactionType = request.TransactionType,
            ReferenceId = request.ReferenceId,
            ReferenceNumber = request.ReferenceNumber,
            CustomerOrMemberName = request.CustomerOrMemberName,
            OriginalAmount = request.OriginalAmount,
            ProposedAmount = request.ProposedAmount,
            OriginalPaymentMethod = request.OriginalPaymentMethod,
            ProposedPaymentMethod = request.ProposedPaymentMethod,
            ActionType = request.ActionType,
            Reason = request.Reason,
            RequestedBy = request.RequestedBy,
            RequestedDate = request.RequestedDate,
            Status = request.Status
        };
    }

    public async Task<List<FinancialAdjustmentRequestDto>> GetAdjustmentRequestsAsync(AdjustmentApprovalStatus? status = null, int? branchId = null)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var query = _db.FinancialAdjustmentRequests
            .Include(r => r.Branch)
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        var list = await query.OrderByDescending(r => r.RequestedDate).ToListAsync();

        return list.Select(r => new FinancialAdjustmentRequestDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            BranchId = r.BranchId,
            BranchName = r.Branch?.BranchName ?? "HQ / Branch",
            TransactionType = r.TransactionType,
            ReferenceId = r.ReferenceId,
            ReferenceNumber = r.ReferenceNumber,
            CustomerOrMemberName = r.CustomerOrMemberName,
            OriginalAmount = r.OriginalAmount,
            ProposedAmount = r.ProposedAmount,
            OriginalPaymentMethod = r.OriginalPaymentMethod,
            ProposedPaymentMethod = r.ProposedPaymentMethod,
            ActionType = r.ActionType,
            Reason = r.Reason,
            RequestedBy = r.RequestedBy,
            RequestedDate = r.RequestedDate,
            Status = r.Status,
            ReviewedBy = r.ReviewedBy,
            ReviewedDate = r.ReviewedDate,
            ReviewNotes = r.ReviewNotes
        }).ToList();
    }

    public async Task<bool> ApproveAdjustmentRequestAsync(int requestId, string approvedBy, string? reviewNotes = null)
    {
        var request = await _db.FinancialAdjustmentRequests.FindAsync(requestId);
        if (request == null || request.Status != AdjustmentApprovalStatus.Pending) return false;

        request.Status = AdjustmentApprovalStatus.Approved;
        request.ReviewedBy = approvedBy;
        request.ReviewedDate = DateTime.UtcNow;
        request.ReviewNotes = reviewNotes ?? "Approved by HQ Admin.";

        if (request.TransactionType == RevenueTransactionType.MemberFee)
        {
            var sub = await _db.MemberSubscriptions.FindAsync(request.ReferenceId);
            if (sub != null)
            {
                sub.IsAdjustmentPending = false;

                if (request.ActionType == AdjustmentActionType.VoidRefund)
                {
                    sub.IsVoided = true;
                    sub.VoidReason = $"Voided per HQ Approval: {request.Reason}";
                    sub.Status = MemberStatus.Frozen;
                }
                else if (request.ActionType == AdjustmentActionType.AmountCorrection)
                {
                    sub.FinalAmountPaid = request.ProposedAmount;
                }
                else if (request.ActionType == AdjustmentActionType.PaymentMethodCorrection && request.ProposedPaymentMethod.HasValue)
                {
                    sub.PaymentMethod = request.ProposedPaymentMethod.Value;
                }
            }
        }
        else if (request.TransactionType == RevenueTransactionType.StockSale)
        {
            var order = await _db.SalesOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.ReferenceId);

            if (order != null)
            {
                order.IsAdjustmentPending = false;

                if (request.ActionType == AdjustmentActionType.VoidRefund)
                {
                    order.IsVoided = true;
                    order.VoidReason = $"Voided per HQ Approval: {request.Reason}";

                    // Restore stock quantity to branch inventory for voided sales
                    if (order.BranchId.HasValue)
                    {
                        foreach (var item in order.Items)
                        {
                            var stock = await _db.StockBalances
                                .FirstOrDefaultAsync(s => s.BranchId == order.BranchId.Value && s.ProductId == item.ProductId);

                            if (stock != null)
                            {
                                stock.QuantityOnHand += item.Quantity;
                            }

                            var prod = await _db.Products.FindAsync(item.ProductId);
                            _db.StockTransactions.Add(new StockTransaction
                            {
                                TenantId = order.TenantId,
                                BranchId = order.BranchId.Value,
                                ProductId = item.ProductId,
                                Type = StockTransactionType.Adjustment,
                                Quantity = item.Quantity,
                                UnitCost = prod?.CostPrice ?? 0,
                                UnitSalePrice = prod?.SalePrice ?? 0,
                                ReferenceNumber = $"VOID-{order.InvoiceNumber}",
                                Remarks = $"Restored stock from voided POS sale {order.InvoiceNumber} per HQ Approval"
                            });
                        }
                    }
                }
                else if (request.ActionType == AdjustmentActionType.AmountCorrection)
                {
                    order.TotalAmount = request.ProposedAmount;
                }
                else if (request.ActionType == AdjustmentActionType.PaymentMethodCorrection && request.ProposedPaymentMethod.HasValue)
                {
                    order.PaymentMethod = request.ProposedPaymentMethod.Value;
                }
            }
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: request.TenantId,
            branchId: request.BranchId,
            userId: _currentUser.UserId,
            username: approvedBy,
            userRole: _currentUser.Role?.ToString() ?? "HQAdmin",
            actionType: "ADJUSTMENT_APPROVED",
            module: "Financial",
            entityName: request.TransactionType.ToString(),
            entityId: request.ReferenceNumber,
            details: $"HQ Admin '{approvedBy}' APPROVED adjustment ({request.ActionType}) for {request.ReferenceNumber}. Notes: {reviewNotes}",
            newValues: $"Status: Approved, Amount: {request.ProposedAmount:N0} MMK"
        );

        return true;
    }

    public async Task<bool> RejectAdjustmentRequestAsync(int requestId, string rejectedBy, string rejectionReason)
    {
        var request = await _db.FinancialAdjustmentRequests.FindAsync(requestId);
        if (request == null || request.Status != AdjustmentApprovalStatus.Pending) return false;

        request.Status = AdjustmentApprovalStatus.Rejected;
        request.ReviewedBy = rejectedBy;
        request.ReviewedDate = DateTime.UtcNow;
        request.ReviewNotes = rejectionReason;

        if (request.TransactionType == RevenueTransactionType.MemberFee)
        {
            var sub = await _db.MemberSubscriptions.FindAsync(request.ReferenceId);
            if (sub != null)
            {
                sub.IsAdjustmentPending = false;
            }
        }
        else if (request.TransactionType == RevenueTransactionType.StockSale)
        {
            var order = await _db.SalesOrders.FindAsync(request.ReferenceId);
            if (order != null)
            {
                order.IsAdjustmentPending = false;
            }
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: request.TenantId,
            branchId: request.BranchId,
            userId: _currentUser.UserId,
            username: rejectedBy,
            userRole: _currentUser.Role?.ToString() ?? "HQAdmin",
            actionType: "ADJUSTMENT_REJECTED",
            module: "Financial",
            entityName: request.TransactionType.ToString(),
            entityId: request.ReferenceNumber,
            details: $"HQ Admin '{rejectedBy}' REJECTED adjustment for {request.ReferenceNumber}. Reason: {rejectionReason}",
            newValues: "Status: Rejected"
        );

        return true;
    }

    public async Task<int> GetPendingAdjustmentsCountAsync()
    {
        int tenantId = _currentUser.TenantId ?? 1;
        return await _db.FinancialAdjustmentRequests
            .CountAsync(r => r.TenantId == tenantId && r.Status == AdjustmentApprovalStatus.Pending);
    }
}
