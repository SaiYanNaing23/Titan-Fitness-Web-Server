using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Tenant : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public decimal RatePerActiveMember { get; set; } = 1500m; // Default monthly rate per active member in MMK
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public TenantSubscription? Subscription { get; set; }
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<StaffUser> StaffUsers { get; set; } = new List<StaffUser>();
    public ICollection<TenantMonthlyInvoice> MonthlyInvoices { get; set; } = new List<TenantMonthlyInvoice>();
}

public class TenantSubscription : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int MaxBranches { get; set; } = 5;
    public int MaxStaffAccounts { get; set; } = 20;
    public bool InventoryModuleEnabled { get; set; } = true;
    public bool MultiBranchAccessEnabled { get; set; } = true;
    public bool TurnstileIntegrationEnabled { get; set; } = false;
    public DateTime? EffectiveUntil { get; set; }
}

public class TenantMonthlyInvoice : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public int ActiveMemberCount { get; set; }
    public decimal RatePerMember { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;
    public DateTime? PaidAt { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}
