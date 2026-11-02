using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Branch : BaseEntity, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Geofencing Coordinates
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 50;

    public ICollection<StaffUser> StaffUsers { get; set; } = new List<StaffUser>();
    public ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
    public ICollection<MemberCheckIn> CheckIns { get; set; } = new List<MemberCheckIn>();
}

public class StaffUser : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.FrontDeskStaff;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public class Holiday : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; } // Null if company-wide holiday
    public Branch? Branch { get; set; }

    public string Title { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public string? Description { get; set; }
}
