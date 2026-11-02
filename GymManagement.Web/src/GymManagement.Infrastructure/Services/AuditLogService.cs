using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(GymDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(string actionType, string module, string entityName, string entityId, string details, string? oldValues = null, string? newValues = null)
    {
        var tenantId = _currentUserService.TenantId ?? 1;
        var branchId = _currentUserService.BranchId;
        var userId = _currentUserService.UserId;
        var username = _currentUserService.Username ?? "System";
        var userRole = _currentUserService.Role?.ToString() ?? "Staff";

        await LogAsync(tenantId, branchId, userId, username, userRole, actionType, module, entityName, entityId, details, oldValues, newValues);
    }

    public async Task LogAsync(int tenantId, int? branchId, int? userId, string username, string userRole, string actionType, string module, string entityName, string entityId, string details, string? oldValues = null, string? newValues = null)
    {
        var log = new AuditLog
        {
            TenantId = tenantId,
            BranchId = branchId,
            UserId = userId,
            Username = username,
            UserRole = userRole,
            ActionType = actionType,
            Module = module,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            OldValues = oldValues,
            NewValues = newValues,
            Timestamp = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(int? tenantId = null, int? branchId = null, string? module = null, string? actionType = null, string? search = null, int limit = 100)
    {
        var query = _db.AuditLogs.AsNoTracking().Include(l => l.Tenant).Include(l => l.Branch).AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId == tenantId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(l => l.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(l => l.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(l => l.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.Username.ToLower().Contains(s) ||
                                     l.Details.ToLower().Contains(s) ||
                                     l.EntityId.ToLower().Contains(s) ||
                                     l.EntityName.ToLower().Contains(s));
        }

        return await query.OrderByDescending(l => l.Timestamp).Take(limit).ToListAsync();
    }
}
