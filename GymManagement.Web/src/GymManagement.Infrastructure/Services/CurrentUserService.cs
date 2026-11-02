using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Enums;

namespace GymManagement.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public int? TenantId { get; private set; }
    public int? BranchId { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public UserRole? Role { get; private set; }

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;
    public bool IsTenantAdmin => Role == UserRole.TenantAdmin;
    public bool IsBranchStaff => Role == UserRole.FrontDeskStaff || Role == UserRole.BranchManager;

    public void SetContext(int? tenantId, int? branchId, int? userId, string? username, UserRole? role)
    {
        TenantId = tenantId;
        BranchId = branchId;
        UserId = userId;
        Username = username;
        Role = role;
    }
}
