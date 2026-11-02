using GymManagement.Domain.Enums;

namespace GymManagement.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? TenantId { get; }
    int? BranchId { get; }
    int? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsSuperAdmin { get; }
    bool IsTenantAdmin { get; }
    bool IsBranchStaff { get; }

    void SetContext(int? tenantId, int? branchId, int? userId, string? username, UserRole? role);
}
