using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

public class AuditLog : BaseEntity, IMustHaveTenant, IMayHaveBranch
{
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int? UserId { get; set; }
    public string Username { get; set; } = "System";
    public string UserRole { get; set; } = "Staff";

    public string ActionType { get; set; } = string.Empty; // e.g. CREATE, UPDATE, PRICE_CHANGE, CHECKIN_OVERRIDE, FEE_COLLECTION, SETTLED
    public string Module { get; set; } = string.Empty;     // e.g. Billing, AccessControl, Inventory, Membership, Security
    public string EntityName { get; set; } = string.Empty; // e.g. Member, Product, SalesOrder, TenantMonthlyInvoice
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string IpAddress { get; set; } = "127.0.0.1";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
