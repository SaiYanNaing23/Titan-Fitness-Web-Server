using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Member : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; } // Home Branch
    public Branch? HomeBranch { get; set; }

    public string MemberCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NrcOrPassport { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? PhotoUrl { get; set; }
    public string? MedicalNotes { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.Active;

    public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
    public ICollection<MemberCheckIn> CheckIns { get; set; } = new List<MemberCheckIn>();
}

public class MembershipPlan : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsMultiBranchAllowed { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public class MemberPromotion : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string PromotionTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DiscountPercent { get; set; } // e.g. 15%
    public decimal DiscountAmount { get; set; }  // e.g. 5,000 MMK
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsExpired => DateTime.UtcNow > ValidTo;
    public bool IsUpcoming => DateTime.UtcNow < ValidFrom;
    public bool IsCurrentlyValid => IsActive && !IsExpired && !IsUpcoming;
}

public class MemberSubscription : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; } // Branch where subscription was purchased
    public Branch? Branch { get; set; }

    public int MemberId { get; set; }
    public Member? Member { get; set; }

    public int MembershipPlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountApplied { get; set; }
    public decimal FinalAmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public string? Notes { get; set; }
    public bool IsAdjustmentPending { get; set; } = false;
    public bool IsVoided { get; set; } = false;
    public string? VoidReason { get; set; }
}

public class MemberCheckIn : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; } // Branch where check-in occurred
    public Branch? Branch { get; set; }

    public int MemberId { get; set; }
    public Member? Member { get; set; }

    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public CheckInStatus Status { get; set; } = CheckInStatus.Accepted;
    public string? DenialReason { get; set; }

    // Geofencing and Method Tracking
    public CheckInMethod Method { get; set; } = CheckInMethod.ManualKioskScan;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceMeters { get; set; }
    public int? DurationMinutes => CheckOutTime.HasValue ? (int)(CheckOutTime.Value - CheckInTime).TotalMinutes : null;
}
