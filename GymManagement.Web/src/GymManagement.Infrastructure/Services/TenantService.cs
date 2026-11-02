using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public TenantService(GymDbContext db, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<List<TenantDto>> GetTenantsAsync()
    {
        var tenants = await _db.Tenants
            .Include(t => t.Branches)
            .Include(t => t.Subscription)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var result = new List<TenantDto>();
        foreach (var t in tenants)
        {
            var activeMembers = await _db.Members
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == t.Id && m.Status == MemberStatus.Active)
                .CountAsync();

            result.Add(new TenantDto
            {
                Id = t.Id,
                CompanyName = t.CompanyName,
                Code = t.Code,
                ContactPerson = t.ContactPerson,
                ContactPhone = t.ContactPhone,
                ContactEmail = t.ContactEmail,
                RatePerActiveMember = t.RatePerActiveMember,
                Status = t.Status,
                BranchCount = t.Branches.Count,
                ActiveMemberCount = activeMembers,
                CreatedAt = t.CreatedAt,
                MaxBranches = t.Subscription?.MaxBranches ?? 5,
                MaxStaffAccounts = t.Subscription?.MaxStaffAccounts ?? 20,
                InventoryModuleEnabled = t.Subscription?.InventoryModuleEnabled ?? true,
                MultiBranchAccessEnabled = t.Subscription?.MultiBranchAccessEnabled ?? true
            });
        }

        return result;
    }

    public async Task<TenantDto?> GetTenantByIdAsync(int id)
    {
        var t = await _db.Tenants
            .Include(x => x.Branches)
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return null;

        var activeMembers = await _db.Members
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == t.Id && m.Status == MemberStatus.Active)
            .CountAsync();

        return new TenantDto
        {
            Id = t.Id,
            CompanyName = t.CompanyName,
            Code = t.Code,
            ContactPerson = t.ContactPerson,
            ContactPhone = t.ContactPhone,
            ContactEmail = t.ContactEmail,
            RatePerActiveMember = t.RatePerActiveMember,
            Status = t.Status,
            BranchCount = t.Branches.Count,
            ActiveMemberCount = activeMembers,
            CreatedAt = t.CreatedAt,
            MaxBranches = t.Subscription?.MaxBranches ?? 5,
            MaxStaffAccounts = t.Subscription?.MaxStaffAccounts ?? 20,
            InventoryModuleEnabled = t.Subscription?.InventoryModuleEnabled ?? true,
            MultiBranchAccessEnabled = t.Subscription?.MultiBranchAccessEnabled ?? true
        };
    }

    public async Task<TenantDto> CreateTenantAsync(TenantDto dto)
    {
        var tenant = new Tenant
        {
            CompanyName = dto.CompanyName,
            Code = dto.Code.ToUpper(),
            ContactPerson = dto.ContactPerson,
            ContactPhone = dto.ContactPhone,
            ContactEmail = dto.ContactEmail,
            RatePerActiveMember = dto.RatePerActiveMember > 0 ? dto.RatePerActiveMember : 1500m,
            Status = TenantStatus.Active,
            Subscription = new TenantSubscription
            {
                MaxBranches = dto.MaxBranches > 0 ? dto.MaxBranches : 5,
                MaxStaffAccounts = dto.MaxStaffAccounts > 0 ? dto.MaxStaffAccounts : 20,
                InventoryModuleEnabled = dto.InventoryModuleEnabled,
                MultiBranchAccessEnabled = dto.MultiBranchAccessEnabled
            }
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            actionType: "CREATE",
            module: "Tenant",
            entityName: "Tenant",
            entityId: tenant.Id.ToString(),
            details: $"Created tenant '{tenant.CompanyName}' (Code: {tenant.Code}) with base rate {tenant.RatePerActiveMember:N0} MMK/member.",
            newValues: $"Rate: {tenant.RatePerActiveMember}, Branches: {tenant.Subscription.MaxBranches}"
        );

        dto.Id = tenant.Id;
        return dto;
    }

    public async Task UpdateTenantAsync(TenantDto dto)
    {
        var tenant = await _db.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == dto.Id);

        if (tenant == null) return;

        var oldRate = tenant.RatePerActiveMember;
        var oldStatus = tenant.Status;

        tenant.CompanyName = dto.CompanyName;
        tenant.ContactPerson = dto.ContactPerson;
        tenant.ContactPhone = dto.ContactPhone;
        tenant.ContactEmail = dto.ContactEmail;
        tenant.RatePerActiveMember = dto.RatePerActiveMember;
        tenant.Status = dto.Status;
        tenant.UpdatedAt = DateTime.UtcNow;

        if (tenant.Subscription != null)
        {
            tenant.Subscription.MaxBranches = dto.MaxBranches;
            tenant.Subscription.MaxStaffAccounts = dto.MaxStaffAccounts;
            tenant.Subscription.InventoryModuleEnabled = dto.InventoryModuleEnabled;
            tenant.Subscription.MultiBranchAccessEnabled = dto.MultiBranchAccessEnabled;
            tenant.Subscription.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (oldRate != dto.RatePerActiveMember)
        {
            await _auditLog.LogAsync(
                actionType: "PRICE_CHANGE",
                module: "Billing",
                entityName: "Tenant",
                entityId: tenant.Id.ToString(),
                details: $"Modified billing rate for tenant '{tenant.CompanyName}' from {oldRate:N0} MMK to {dto.RatePerActiveMember:N0} MMK per active member.",
                oldValues: $"{oldRate:N0} MMK",
                newValues: $"{dto.RatePerActiveMember:N0} MMK"
            );
        }

        if (oldStatus != dto.Status)
        {
            await _auditLog.LogAsync(
                actionType: "UPDATE",
                module: "Tenant",
                entityName: "Tenant",
                entityId: tenant.Id.ToString(),
                details: $"Changed status for tenant '{tenant.CompanyName}' from {oldStatus} to {dto.Status}.",
                oldValues: oldStatus.ToString(),
                newValues: dto.Status.ToString()
            );
        }
    }

    public async Task<List<TenantMonthlyInvoiceDto>> CalculateMonthlyInvoicesAsync(int year, int month)
    {
        var tenants = await _db.Tenants.Where(t => t.Status == TenantStatus.Active).ToListAsync();
        var invoices = new List<TenantMonthlyInvoiceDto>();

        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        foreach (var tenant in tenants)
        {
            // Count unique active members who had active subscriptions or check-ins during this calendar month
            var activeMemberCount = await _db.Members
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenant.Id)
                .Where(m => m.Subscriptions.Any(s => s.StartDate <= monthEnd && s.EndDate >= monthStart)
                            || m.CheckIns.Any(c => c.CheckInTime >= monthStart && c.CheckInTime <= monthEnd))
                .CountAsync();

            var existingInvoice = await _db.TenantMonthlyInvoices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.TenantId == tenant.Id && i.BillingYear == year && i.BillingMonth == month);

            decimal totalAmount = activeMemberCount * tenant.RatePerActiveMember;

            if (existingInvoice != null)
            {
                // Historical record preservation: Preserve existing invoice rate!
                // If invoice is already paid or was previously issued, retain its historical RatePerMember.
                if (existingInvoice.PaymentStatus != InvoicePaymentStatus.Paid)
                {
                    existingInvoice.ActiveMemberCount = activeMemberCount;
                    existingInvoice.TotalAmount = activeMemberCount * existingInvoice.RatePerMember;
                    existingInvoice.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // New monthly calculation applies the tenant's current RatePerActiveMember
                var newInvoice = new TenantMonthlyInvoice
                {
                    TenantId = tenant.Id,
                    BillingYear = year,
                    BillingMonth = month,
                    ActiveMemberCount = activeMemberCount,
                    RatePerMember = tenant.RatePerActiveMember,
                    TotalAmount = activeMemberCount * tenant.RatePerActiveMember,
                    PaymentStatus = InvoicePaymentStatus.Unpaid
                };
                _db.TenantMonthlyInvoices.Add(newInvoice);
            }

            invoices.Add(new TenantMonthlyInvoiceDto
            {
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                BillingYear = year,
                BillingMonth = month,
                ActiveMemberCount = activeMemberCount,
                RatePerMember = existingInvoice?.RatePerMember ?? tenant.RatePerActiveMember,
                TotalAmount = existingInvoice?.TotalAmount ?? (activeMemberCount * tenant.RatePerActiveMember),
                PaymentStatus = existingInvoice?.PaymentStatus ?? InvoicePaymentStatus.Unpaid
            });
        }

        await _db.SaveChangesAsync();
        return invoices;
    }

    public async Task<List<TenantMonthlyInvoiceDto>> GetMonthlyInvoicesAsync(int? tenantId = null)
    {
        var query = _db.TenantMonthlyInvoices
            .Include(i => i.Tenant)
            .AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(i => i.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(i => i.BillingYear)
            .ThenByDescending(i => i.BillingMonth)
            .Select(i => new TenantMonthlyInvoiceDto
            {
                Id = i.Id,
                TenantId = i.TenantId,
                CompanyName = i.Tenant != null ? i.Tenant.CompanyName : "Tenant #" + i.TenantId,
                BillingYear = i.BillingYear,
                BillingMonth = i.BillingMonth,
                ActiveMemberCount = i.ActiveMemberCount,
                RatePerMember = i.RatePerMember,
                TotalAmount = i.TotalAmount,
                PaymentStatus = i.PaymentStatus,
                PaidAt = i.PaidAt,
                PaymentReference = i.PaymentReference,
                Notes = i.Notes
            })
            .ToListAsync();
    }

    public async Task RecordInvoicePaymentAsync(int invoiceId, string paymentReference, string? notes)
    {
        var invoice = await _db.TenantMonthlyInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return;

        invoice.PaymentStatus = InvoicePaymentStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaymentReference = paymentReference;
        invoice.Notes = notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: invoice.TenantId,
            branchId: null,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "SuperAdmin",
            userRole: _currentUser.Role?.ToString() ?? "SuperAdmin",
            actionType: "SETTLED",
            module: "Billing",
            entityName: "TenantMonthlyInvoice",
            entityId: invoice.Id.ToString(),
            details: $"Settled postpaid invoice #{invoice.Id} for {invoice.BillingYear}-{invoice.BillingMonth:D2} ({invoice.TotalAmount:N0} MMK). Payment Ref: {paymentReference}.",
            newValues: $"Paid: {invoice.TotalAmount:N0} MMK, Ref: {paymentReference}"
        );
    }

    public async Task<StaffUserDto?> GetTenantAdminAsync(int tenantId)
    {
        var admin = await _db.StaffUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && (u.Role == UserRole.TenantAdmin || u.Role == UserRole.SuperAdmin));

        if (admin == null) return null;

        return new StaffUserDto
        {
            Id = admin.Id,
            TenantId = admin.TenantId,
            BranchId = admin.BranchId,
            Username = admin.Username,
            FullName = admin.FullName,
            Email = admin.Email,
            Phone = admin.Phone,
            Role = admin.Role,
            IsActive = admin.IsActive,
            LastLoginAt = admin.LastLoginAt
        };
    }

    public async Task<bool> ResetTenantAdminPasswordAsync(int tenantId, string newPassword)
    {
        var adminUser = await _db.StaffUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role == UserRole.TenantAdmin);

        if (adminUser == null)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return false;

            adminUser = new StaffUser
            {
                TenantId = tenantId,
                Username = $"{tenant.Code.ToLower()}admin",
                FullName = $"{tenant.CompanyName} Administrator",
                Email = tenant.ContactEmail,
                Phone = tenant.ContactPhone,
                Role = UserRole.TenantAdmin,
                IsActive = true,
                PasswordHash = HashPassword(newPassword)
            };
            _db.StaffUsers.Add(adminUser);
        }
        else
        {
            adminUser.PasswordHash = HashPassword(newPassword);
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            actionType: "TENANT_PASSWORD_RESET",
            module: "TenantSecurity",
            entityName: "TenantAdmin",
            entityId: tenantId.ToString(),
            details: $"Super Admin reset password for Tenant ID {tenantId} (Admin User: '{adminUser.Username}')."
        );

        return true;
    }

    private string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "GymSalt");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
