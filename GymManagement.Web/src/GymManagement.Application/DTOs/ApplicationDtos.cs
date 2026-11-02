using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs;

public class TenantDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public decimal RatePerActiveMember { get; set; }
    public TenantStatus Status { get; set; }
    public int BranchCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Subscription details
    public int MaxBranches { get; set; } = 5;
    public int MaxStaffAccounts { get; set; } = 20;
    public bool InventoryModuleEnabled { get; set; } = true;
    public bool MultiBranchAccessEnabled { get; set; } = true;
}

public class TenantMonthlyInvoiceDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public int ActiveMemberCount { get; set; }
    public decimal RatePerMember { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoicePaymentStatus PaymentStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

public class BranchDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int StaffCount { get; set; }
    public int TodayCheckIns { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 50;
}

public class StaffUserDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class MemberDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }
    public string? HomeBranchName { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Gender { get; set; }
    public string? PhotoUrl { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public MemberStatus Status { get; set; }

    // Active subscription info
    public string? CurrentPlanName { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpiringSoon => DaysRemaining is >= 0 and <= 3;
    public bool IsOverdue => DaysRemaining < 0;

    // Financial audit & adjustment tracking
    public int? LatestSubscriptionId { get; set; }
    public decimal LatestSubscriptionAmount { get; set; }
    public PaymentMethod? LatestPaymentMethod { get; set; }
    public bool IsAdjustmentPending { get; set; }
}

public class CheckInResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MemberDto? Member { get; set; }
    public DateTime CheckInTime { get; set; }
    public bool IsCheckOut { get; set; }
    public string? PlanName { get; set; }
    public int DaysRemaining { get; set; }
    public bool WarningExpiringSoon { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = "pcs";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? PromoPrice { get; set; }
    public int TotalStock { get; set; }
    public int BranchStock { get; set; }
    public int LowStockAlertQuantity { get; set; }
    public bool IsLowStock => BranchStock <= LowStockAlertQuantity;
    public List<BranchStockDetailDto> BranchStocks { get; set; } = new();
}

public class BranchStockDetailDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
}

public class StockPnLDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal TotalSalesRevenue { get; set; }
    public decimal TotalCostOfGoodsSold { get; set; }
    public decimal GrossProfit => TotalSalesRevenue - TotalCostOfGoodsSold;
    public decimal MarginPercent => TotalSalesRevenue > 0 ? Math.Round((GrossProfit / TotalSalesRevenue) * 100, 1) : 0;
    public int CurrentStock { get; set; }
    public decimal InventoryValuation => CurrentStock * TotalCostOfGoodsSold / (UnitsSold > 0 ? UnitsSold : 1);
    public List<BranchStockDetailDto> BranchStocks { get; set; } = new();
}

public class GeofenceCheckInRequestDto
{
    public string MemberCodeOrPhone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int? TargetBranchId { get; set; }
}

public class GeofenceCheckInResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MemberDto? Member { get; set; }
    public string? BranchName { get; set; }
    public int BranchId { get; set; }
    public double DistanceMeters { get; set; }
    public int AllowedRadiusMeters { get; set; }
    public bool InsideGeofence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsCheckOut { get; set; }
    public int? WorkoutDurationMinutes { get; set; }
    public string? FormattedDuration { get; set; }
}

public class MobileBranchGeofenceDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }
}
