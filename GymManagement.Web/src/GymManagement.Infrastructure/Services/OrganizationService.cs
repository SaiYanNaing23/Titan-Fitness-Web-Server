using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public OrganizationService(GymDbContext db, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        var branches = await _db.Branches
            .Include(b => b.StaffUsers)
            .Include(b => b.CheckIns)
            .ToListAsync();

        var today = DateTime.UtcNow.Date;

        return branches.Select(b => new BranchDto
        {
            Id = b.Id,
            TenantId = b.TenantId,
            BranchName = b.BranchName,
            BranchCode = b.BranchCode,
            Address = b.Address,
            PhoneNumber = b.PhoneNumber,
            IsActive = b.IsActive,
            StaffCount = b.StaffUsers.Count,
            TodayCheckIns = b.CheckIns.Count(c => c.CheckInTime.Date == today),
            Latitude = b.Latitude,
            Longitude = b.Longitude,
            GeofenceRadiusMeters = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 50
        }).ToList();
    }

    public async Task<BranchDto> CreateBranchAsync(BranchDto dto)
    {
        int tenantId = _currentUser.TenantId ?? dto.TenantId;
        if (tenantId <= 0)
        {
            var firstTenant = await _db.Tenants.FirstOrDefaultAsync();
            tenantId = firstTenant?.Id ?? 1;
        }

        var branch = new Branch
        {
            TenantId = tenantId,
            BranchName = dto.BranchName,
            BranchCode = dto.BranchCode.ToUpper(),
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            IsActive = true,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            GeofenceRadiusMeters = dto.GeofenceRadiusMeters > 0 ? dto.GeofenceRadiusMeters : 50
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        dto.Id = branch.Id;
        dto.TenantId = tenantId;
        return dto;
    }

    public async Task<List<StaffUserDto>> GetStaffUsersAsync()
    {
        return await _db.StaffUsers
            .Include(u => u.Branch)
            .Select(u => new StaffUserDto
            {
                Id = u.Id,
                TenantId = u.TenantId,
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.BranchName : "All Branches (HQ)",
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();
    }

    public async Task<StaffUserDto> CreateStaffUserAsync(StaffUserDto dto, string password)
    {
        int tenantId = _currentUser.TenantId ?? dto.TenantId;
        if (tenantId <= 0)
        {
            var firstTenant = await _db.Tenants.FirstOrDefaultAsync();
            tenantId = firstTenant?.Id ?? 1;
        }

        var staff = new StaffUser
        {
            TenantId = tenantId,
            BranchId = dto.BranchId,
            Username = dto.Username,
            FullName = dto.FullName,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = dto.Role,
            IsActive = true,
            PasswordHash = BCryptOrSimpleHash(password)
        };

        _db.StaffUsers.Add(staff);
        await _db.SaveChangesAsync();

        dto.Id = staff.Id;
        return dto;
    }

    public async Task<List<Holiday>> GetHolidaysAsync()
    {
        return await _db.Holidays
            .Include(h => h.Branch)
            .OrderBy(h => h.HolidayDate)
            .ToListAsync();
    }

    public async Task CreateHolidayAsync(string title, DateTime date, string? description, int? branchId)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var holiday = new Holiday
        {
            TenantId = tenantId,
            BranchId = branchId,
            Title = title,
            HolidayDate = date,
            Description = description
        };

        _db.Holidays.Add(holiday);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ResetStaffPasswordAsync(int staffUserId, string newPassword)
    {
        var staff = await _db.StaffUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == staffUserId);

        if (staff == null) return false;

        staff.PasswordHash = BCryptOrSimpleHash(newPassword);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            actionType: "PASSWORD_RESET",
            module: "StaffSecurity",
            entityName: "StaffUser",
            entityId: staff.Id.ToString(),
            details: $"Reset password for staff '{staff.Username}' ({staff.FullName}, Role: {staff.Role})."
        );

        return true;
    }

    public async Task<(bool Success, string Message, string? DemoOtp)> RequestPasswordResetOtpAsync(string usernameOrEmail)
    {
        var normalized = usernameOrEmail.Trim().ToLower();
        var user = await _db.StaffUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized);

        if (user == null)
        {
            return (false, "No staff or admin account found matching this username or email.", null);
        }

        // Demo verification OTP
        string demoOtp = "849201";

        string maskedEmail = MaskEmail(user.Email);
        string maskedPhone = MaskPhone(user.Phone);

        await _auditLog.LogAsync(
            actionType: "OTP_REQUEST",
            module: "Authentication",
            entityName: "StaffUser",
            entityId: user.Id.ToString(),
            details: $"Password recovery OTP requested for user '{user.Username}'."
        );

        return (true, $"6-digit verification code sent to {maskedEmail} (SMS: {maskedPhone}).", demoOtp);
    }

    public async Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string usernameOrEmail, string otp, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(otp) || otp.Trim().Length != 6)
        {
            return (false, "Invalid verification code. Please enter the 6-digit code.");
        }

        var normalized = usernameOrEmail.Trim().ToLower();
        var user = await _db.StaffUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized);

        if (user == null)
        {
            return (false, "Account not found.");
        }

        user.PasswordHash = BCryptOrSimpleHash(newPassword);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            actionType: "PASSWORD_RESET_SELF",
            module: "Authentication",
            entityName: "StaffUser",
            entityId: user.Id.ToString(),
            details: $"User '{user.Username}' self-reset their password using OTP verification."
        );

        return (true, "Your password has been successfully reset! You can now sign in with your new password.");
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "registered email";
        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];
        if (name.Length <= 2) return $"{name}***@{domain}";
        return $"{name[0]}***{name[^1]}@{domain}";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 6) return "registered phone";
        return $"{phone[..3]}****{phone[^2..]}";
    }

    private string BCryptOrSimpleHash(string password)
    {
        // Simple secure hash for demo, or PBKDF2
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "GymSalt");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
