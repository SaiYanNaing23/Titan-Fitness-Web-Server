using GymManagement.Application.DTOs;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.Services;

public interface ITenantService
{
    Task<List<TenantDto>> GetTenantsAsync();
    Task<TenantDto?> GetTenantByIdAsync(int id);
    Task<TenantDto> CreateTenantAsync(TenantDto dto);
    Task UpdateTenantAsync(TenantDto dto);
    Task<List<TenantMonthlyInvoiceDto>> CalculateMonthlyInvoicesAsync(int year, int month);
    Task<List<TenantMonthlyInvoiceDto>> GetMonthlyInvoicesAsync(int? tenantId = null);
    Task RecordInvoicePaymentAsync(int invoiceId, string paymentReference, string? notes);
    Task<bool> ResetTenantAdminPasswordAsync(int tenantId, string newPassword);
    Task<StaffUserDto?> GetTenantAdminAsync(int tenantId);
}

public interface IOrganizationService
{
    Task<List<BranchDto>> GetBranchesAsync();
    Task<BranchDto> CreateBranchAsync(BranchDto dto);
    Task<List<StaffUserDto>> GetStaffUsersAsync();
    Task<StaffUserDto> CreateStaffUserAsync(StaffUserDto dto, string password);
    Task<bool> ResetStaffPasswordAsync(int staffUserId, string newPassword);
    Task<(bool Success, string Message, string? DemoOtp)> RequestPasswordResetOtpAsync(string usernameOrEmail);
    Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string usernameOrEmail, string otp, string newPassword);
    Task<List<Holiday>> GetHolidaysAsync();
    Task CreateHolidayAsync(string title, DateTime date, string? description, int? branchId);
}

public interface IMembershipService
{
    Task<List<MemberDto>> GetMembersAsync(string? search = null);
    Task<MemberDto?> GetMemberByIdAsync(int id);
    Task<MemberDto> RegisterMemberAsync(MemberDto dto, int? planId = null, PaymentMethod method = PaymentMethod.Cash);
    Task<List<MembershipPlan>> GetPlansAsync();
    Task<MembershipPlan> CreatePlanAsync(string planName, int durationDays, decimal price, bool isMultiBranch);
    Task<MemberSubscription> EnrollSubscriptionAsync(int memberId, int planId, decimal discount, PaymentMethod method);
    Task<CheckInResultDto> ProcessCheckInAsync(string codeOrPhone, int currentBranchId);
    Task<bool> ProcessCheckOutAsync(int checkInId);
    Task<List<MemberDto>> GetDuePeriodAlertsAsync(int daysThreshold = 7);
    Task<List<MemberCheckIn>> GetTodayCheckInsAsync(int? branchId = null);
    Task<List<MemberPromotion>> GetPromotionsAsync();
    Task<MemberPromotion> CreatePromotionAsync(string title, string? description, decimal discountPercent, decimal discountAmount, DateTime validFrom, DateTime validTo);
    Task<MemberPromotion> UpdatePromotionAsync(int id, string title, string? description, decimal discountPercent, decimal discountAmount, DateTime validFrom, DateTime validTo, bool isActive);
    Task DeletePromotionAsync(int id);
    Task<GeofenceCheckInResultDto> ProcessGeofenceCheckInAsync(string codeOrPhone, double userLat, double userLon, int? branchId = null);
    Task<GeofenceCheckInResultDto> ProcessGeofenceCheckOutAsync(string codeOrPhone, double userLat, double userLon, int? branchId = null);
    Task<List<MobileBranchGeofenceDto>> GetMobileGeofencesAsync();
    Task<List<MemberCheckIn>> GetMemberCheckInsHistoryAsync(int memberId);
    Task<MemberCheckIn?> GetActiveCheckInSessionAsync(int memberId);
}

public interface IInventoryService
{
    Task<List<ProductDto>> GetProductsAsync();
    Task<ProductDto> CreateProductAsync(ProductDto dto);
    Task UpdateProductPriceAsync(int productId, decimal newSalePrice, decimal newCostPrice, string reason);
    Task CreatePromotionAsync(string title, int productId, decimal promoPrice, DateTime validFrom, DateTime validTo);
    Task<List<StockBalance>> GetStockBalancesAsync(int? branchId = null);
    Task<StockRequest> CreateStockRequestAsync(int requestingBranchId, int? fulfillingBranchId, List<(int productId, int qty)> items, string requestedBy);
    Task<List<StockRequest>> GetStockRequestsAsync();
    Task ApproveStockRequestAsync(int requestId, string approvedBy, Dictionary<int, int>? adjustedApprovedQuantities = null, string? notes = null);
    Task<StockRequest> DispatchStockFromHqAsync(int sourceBranchId, int destBranchId, List<(int productId, int qty)> items, string dispatchedBy, string? notes);
    Task ConfirmStockReceiptAsync(int requestId, string receivedBy);
    Task<SalesOrder> ProcessSalesOrderAsync(int? memberId, int branchId, List<(int productId, int qty, decimal price)> items, PaymentMethod method, string cashier);
    Task<List<StockPnLDto>> GetStockPnLAsync();
}

public interface IAuditLogService
{
    Task LogAsync(string actionType, string module, string entityName, string entityId, string details, string? oldValues = null, string? newValues = null);
    Task LogAsync(int tenantId, int? branchId, int? userId, string username, string userRole, string actionType, string module, string entityName, string entityId, string details, string? oldValues = null, string? newValues = null);
    Task<List<AuditLog>> GetAuditLogsAsync(int? tenantId = null, int? branchId = null, string? module = null, string? actionType = null, string? search = null, int limit = 100);
}

public interface IFinancialService
{
    Task<DailyRevenueSummaryDto> GetDailyRevenueSummaryAsync(DateTime date, int? branchId = null);
    Task<List<RevenueTransactionDto>> GetRevenueTransactionsAsync(DateTime fromDate, DateTime toDate, int? branchId = null, RevenueTransactionType? type = null, PaymentMethod? paymentMethod = null, string? search = null);
    Task<FinancialAdjustmentRequestDto> SubmitAdjustmentRequestAsync(RevenueTransactionType type, int referenceId, AdjustmentActionType actionType, decimal proposedAmount, PaymentMethod? proposedPaymentMethod, string reason, string requestedBy);
    Task<List<FinancialAdjustmentRequestDto>> GetAdjustmentRequestsAsync(AdjustmentApprovalStatus? status = null, int? branchId = null);
    Task<bool> ApproveAdjustmentRequestAsync(int requestId, string approvedBy, string? reviewNotes = null);
    Task<bool> RejectAdjustmentRequestAsync(int requestId, string rejectedBy, string rejectionReason);
    Task<int> GetPendingAdjustmentsCountAsync();
}

public interface IPaymentNotificationService
{
    event Func<PaymentNotificationDto, Task>? OnPaymentReceived;
    Task NotifyPaymentAsync(PaymentNotificationDto notification);
    List<PaymentNotificationDto> GetRecentNotifications(int? tenantId = null, int limit = 20);
    int GetUnreadCount(int? tenantId = null);
    void MarkAsRead(string notificationId);
    void MarkAllAsRead(int? tenantId = null);
}


