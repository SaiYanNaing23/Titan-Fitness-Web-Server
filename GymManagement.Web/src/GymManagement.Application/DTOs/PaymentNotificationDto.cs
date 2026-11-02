using System;

namespace GymManagement.Application.DTOs;

public class PaymentNotificationDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int TenantId { get; set; }
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = "Main Branch";
    public string TransactionType { get; set; } = "MEMBER_FEE"; // "MEMBER_FEE" or "POS_SALE"
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string CashierOrStaff { get; set; } = "Staff";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - Timestamp;
            if (diff.TotalSeconds < 60) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return Timestamp.ToString("dd MMM, hh:mm tt");
        }
    }
}
