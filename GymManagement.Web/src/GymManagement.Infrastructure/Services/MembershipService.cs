using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Events;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Services;

public class MembershipService : IMembershipService
{
    private readonly GymDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly ICacheService _cache;
    private readonly IPaymentNotificationService _notificationService;

    public MembershipService(
        GymDbContext db, 
        ICurrentUserService currentUser, 
        IAuditLogService auditLog, 
        ICacheService cache,
        IPaymentNotificationService? notificationService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _cache = cache;
        _notificationService = notificationService ?? new NullPaymentNotificationService();
    }

    public async Task<List<MemberDto>> GetMembersAsync(string? search = null)
    {
        var query = _db.Members
            .Include(m => m.HomeBranch)
            .Include(m => m.Subscriptions)
                .ThenInclude(s => s.MembershipPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(m => m.FullName.ToLower().Contains(s)
                                  || m.PhoneNumber.Contains(s)
                                  || m.MemberCode.ToLower().Contains(s));
        }

        var members = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
        var now = DateTime.UtcNow;

        return members.Select(m =>
        {
            var activeSub = m.Subscriptions
                .Where(s => s.Status == MemberStatus.Active && s.EndDate >= now.Date)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            var latestSub = activeSub ?? m.Subscriptions.OrderByDescending(s => s.EndDate).FirstOrDefault();
            var daysRemaining = latestSub != null ? (int)(latestSub.EndDate.Date - now.Date).TotalDays : -1;

            return new MemberDto
            {
                Id = m.Id,
                TenantId = m.TenantId,
                BranchId = m.BranchId,
                HomeBranchName = m.HomeBranch?.BranchName ?? "Main Branch",
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                PhoneNumber = m.PhoneNumber,
                Email = m.Email,
                Gender = m.Gender,
                PhotoUrl = m.PhotoUrl,
                EmergencyContactName = m.EmergencyContactName,
                EmergencyContactPhone = m.EmergencyContactPhone,
                Status = activeSub != null ? MemberStatus.Active : (latestSub != null ? MemberStatus.Expired : m.Status),
                CurrentPlanName = latestSub?.MembershipPlan?.PlanName ?? "No Active Plan",
                SubscriptionEndDate = latestSub?.EndDate,
                DaysRemaining = daysRemaining,
                LatestSubscriptionId = latestSub?.Id,
                LatestSubscriptionAmount = latestSub?.FinalAmountPaid ?? 0,
                LatestPaymentMethod = latestSub?.PaymentMethod,
                IsAdjustmentPending = latestSub?.IsAdjustmentPending ?? false
            };
        }).ToList();
    }

    public async Task<MemberDto?> GetMemberByIdAsync(int id)
    {
        var m = await _db.Members
            .Include(x => x.HomeBranch)
            .Include(x => x.Subscriptions)
                .ThenInclude(s => s.MembershipPlan)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (m == null) return null;

        var now = DateTime.UtcNow;
        var activeSub = m.Subscriptions
            .Where(s => s.Status == MemberStatus.Active && s.EndDate >= now.Date)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        var latestSub = activeSub ?? m.Subscriptions.OrderByDescending(s => s.EndDate).FirstOrDefault();
        var daysRemaining = latestSub != null ? (int)(latestSub.EndDate.Date - now.Date).TotalDays : -1;

        return new MemberDto
        {
            Id = m.Id,
            TenantId = m.TenantId,
            BranchId = m.BranchId,
            HomeBranchName = m.HomeBranch?.BranchName ?? "Main Branch",
            MemberCode = m.MemberCode,
            FullName = m.FullName,
            PhoneNumber = m.PhoneNumber,
            Email = m.Email,
            Gender = m.Gender,
            PhotoUrl = m.PhotoUrl,
            EmergencyContactName = m.EmergencyContactName,
            EmergencyContactPhone = m.EmergencyContactPhone,
            Status = activeSub != null ? MemberStatus.Active : (latestSub != null ? MemberStatus.Expired : m.Status),
            CurrentPlanName = latestSub?.MembershipPlan?.PlanName ?? "No Active Plan",
            SubscriptionEndDate = latestSub?.EndDate,
            DaysRemaining = daysRemaining,
            LatestSubscriptionId = latestSub?.Id,
            LatestSubscriptionAmount = latestSub?.FinalAmountPaid ?? 0,
            LatestPaymentMethod = latestSub?.PaymentMethod,
            IsAdjustmentPending = latestSub?.IsAdjustmentPending ?? false
        };
    }

    public async Task<MemberDto> RegisterMemberAsync(MemberDto dto, int? planId = null, PaymentMethod method = PaymentMethod.Cash)
    {
        int tenantId = _currentUser.TenantId ?? dto.TenantId;
        if (tenantId <= 0)
        {
            var firstTenant = await _db.Tenants.FirstOrDefaultAsync();
            tenantId = firstTenant?.Id ?? 1;
        }

        int? branchId = dto.BranchId ?? _currentUser.BranchId;

        // Auto-generate MemberCode if blank
        string memberCode = dto.MemberCode;
        if (string.IsNullOrWhiteSpace(memberCode))
        {
            var count = await _db.Members.CountAsync();
            memberCode = $"MBR-{(count + 1):D5}";
        }

        var member = new Member
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberCode = memberCode,
            FullName = dto.FullName,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Gender = dto.Gender,
            PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=150&q=80" : dto.PhotoUrl,
            EmergencyContactName = dto.EmergencyContactName,
            EmergencyContactPhone = dto.EmergencyContactPhone,
            Status = MemberStatus.Active
        };

        _db.Members.Add(member);
        member.AddDomainEvent(new MemberEnrolledEvent(member.Id, member.TenantId, member.BranchId, member.FullName, member.MemberCode));
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: member.TenantId,
            branchId: member.BranchId,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Staff",
            userRole: _currentUser.Role?.ToString() ?? "Staff",
            actionType: "CREATE",
            module: "Membership",
            entityName: "Member",
            entityId: member.Id.ToString(),
            details: $"Enrolled new member '{member.FullName}' (Code: {member.MemberCode}, Phone: {member.PhoneNumber})",
            newValues: $"Code: {member.MemberCode}, BranchId: {member.BranchId}"
        );

        if (planId.HasValue && planId.Value > 0)
        {
            await EnrollSubscriptionAsync(member.Id, planId.Value, 0, method);
        }

        dto.Id = member.Id;
        dto.MemberCode = member.MemberCode;
        return dto;
    }

    public async Task<List<MembershipPlan>> GetPlansAsync()
    {
        int tenantId = _currentUser.TenantId ?? 1;
        return await _cache.GetOrCreateAsync($"membership_plans_tenant_{tenantId}", async () =>
        {
            return await _db.MembershipPlans.Where(p => p.IsActive).ToListAsync();
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<MembershipPlan> CreatePlanAsync(string planName, int durationDays, decimal price, bool isMultiBranch)
    {
        int tenantId = _currentUser.TenantId ?? 1;

        var plan = new MembershipPlan
        {
            TenantId = tenantId,
            PlanName = planName,
            DurationDays = durationDays,
            BasePrice = price,
            IsMultiBranchAllowed = isMultiBranch,
            IsActive = true
        };

        _db.MembershipPlans.Add(plan);
        await _db.SaveChangesAsync();

        // Invalidate cached plans for this tenant
        _cache.Remove($"membership_plans_tenant_{tenantId}");

        return plan;
    }

    public async Task<MemberSubscription> EnrollSubscriptionAsync(int memberId, int planId, decimal discount, PaymentMethod method)
    {
        var plan = await _db.MembershipPlans.FindAsync(planId);
        if (plan == null) throw new InvalidOperationException("Membership plan not found.");

        int tenantId = _currentUser.TenantId ?? plan.TenantId;
        var now = DateTime.UtcNow;

        // Check if there is an active subscription; if so, extend from current end date
        var existingSub = await _db.MemberSubscriptions
            .Where(s => s.MemberId == memberId && s.EndDate >= now.Date && s.Status == MemberStatus.Active)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        DateTime startDate = existingSub != null ? existingSub.EndDate.AddDays(1) : now.Date;
        DateTime endDate = startDate.AddDays(plan.DurationDays);
        decimal finalAmount = Math.Max(0, plan.BasePrice - discount);

        var subscription = new MemberSubscription
        {
            TenantId = tenantId,
            BranchId = _currentUser.BranchId,
            MemberId = memberId,
            MembershipPlanId = planId,
            StartDate = startDate,
            EndDate = endDate,
            OriginalPrice = plan.BasePrice,
            DiscountApplied = discount,
            FinalAmountPaid = finalAmount,
            PaymentMethod = method,
            Status = MemberStatus.Active
        };

        _db.MemberSubscriptions.Add(subscription);

        var member = await _db.Members.FindAsync(memberId);
        if (member != null)
        {
            member.Status = MemberStatus.Active;
        }

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: subscription.TenantId,
            branchId: subscription.BranchId,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Staff",
            userRole: _currentUser.Role?.ToString() ?? "Staff",
            actionType: "FEE_COLLECTION",
            module: "Membership",
            entityName: "MemberSubscription",
            entityId: subscription.Id.ToString(),
            details: $"Collected fee for member '{member?.FullName}' - Plan: '{plan.PlanName}', Amount: {finalAmount:N0} MMK ({method}). Valid {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}.",
            newValues: $"Paid: {finalAmount:N0} MMK, Method: {method}, Plan: {plan.PlanName}"
        );

        // Real-Time Notification to Admin
        var branchName = "Main Branch";
        if (subscription.BranchId.HasValue)
        {
            var branch = await _db.Branches.FindAsync(subscription.BranchId.Value);
            if (branch != null) branchName = branch.BranchName;
        }

        await _notificationService.NotifyPaymentAsync(new PaymentNotificationDto
        {
            TenantId = subscription.TenantId,
            BranchId = subscription.BranchId,
            BranchName = branchName,
            TransactionType = "MEMBER_FEE",
            ReferenceNumber = member?.MemberCode ?? $"SUB-{subscription.Id}",
            CustomerName = member?.FullName ?? "Member",
            ItemDescription = $"Membership Fee ({plan.PlanName})",
            Amount = subscription.FinalAmountPaid,
            PaymentMethod = method.ToString(),
            CashierOrStaff = _currentUser.Username ?? "Front Desk Staff",
            Timestamp = DateTime.UtcNow
        });

        return subscription;
    }

    public async Task<CheckInResultDto> ProcessCheckInAsync(string codeOrPhone, int currentBranchId)
    {
        if (string.IsNullOrWhiteSpace(codeOrPhone))
        {
            return new CheckInResultDto { Success = false, Message = "Please scan a QR/Barcode or enter a phone number." };
        }

        var searchKey = codeOrPhone.Trim().ToLower();

        var member = await _db.Members
            .Include(m => m.HomeBranch)
            .Include(m => m.Subscriptions)
                .ThenInclude(s => s.MembershipPlan)
            .FirstOrDefaultAsync(m => m.MemberCode.ToLower() == searchKey || m.PhoneNumber == searchKey);

        if (member == null)
        {
            return new CheckInResultDto { Success = false, Message = $"Member with identifier '{codeOrPhone}' not found." };
        }

        int tenantId = member.TenantId;
        var now = DateTime.UtcNow;

        // Check for active subscription
        var activeSub = member.Subscriptions
            .Where(s => s.Status == MemberStatus.Active && s.EndDate >= now.Date)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        var memberDto = await GetMemberByIdAsync(member.Id);

        // Check if member already checked in today and has not checked out yet (Dual Scan Check-Out)
        var today = now.Date;
        var openCheckIn = await _db.MemberCheckIns
            .Where(c => c.MemberId == member.Id && c.CheckInTime.Date == today && c.CheckOutTime == null)
            .OrderByDescending(c => c.CheckInTime)
            .FirstOrDefaultAsync();

        if (openCheckIn != null)
        {
            // Auto Check-Out
            openCheckIn.CheckOutTime = now;
            await _db.SaveChangesAsync();

            return new CheckInResultDto
            {
                Success = true,
                IsCheckOut = true,
                Message = $"Checked Out: {member.FullName} at {now:hh:mm tt}",
                Member = memberDto,
                CheckInTime = openCheckIn.CheckInTime,
                PlanName = activeSub?.MembershipPlan?.PlanName ?? "Standard",
                DaysRemaining = memberDto?.DaysRemaining ?? 0
            };
        }

        // Validate Membership Status
        if (activeSub == null)
        {
            var deniedLog = new MemberCheckIn
            {
                TenantId = tenantId,
                BranchId = currentBranchId,
                MemberId = member.Id,
                CheckInTime = now,
                Status = CheckInStatus.Denied,
                DenialReason = "Membership Expired / No Active Plan"
            };
            deniedLog.AddDomainEvent(new TurnstileAccessDeniedEvent(member.Id, tenantId, currentBranchId, deniedLog.DenialReason));
            _db.MemberCheckIns.Add(deniedLog);
            await _db.SaveChangesAsync();

            await _auditLog.LogAsync(
                tenantId: tenantId,
                branchId: currentBranchId,
                userId: _currentUser.UserId,
                username: _currentUser.Username ?? "FrontDesk",
                userRole: _currentUser.Role?.ToString() ?? "Staff",
                actionType: "ACCESS_DENIED",
                module: "AccessControl",
                entityName: "Member",
                entityId: member.Id.ToString(),
                details: $"Access Denied for '{member.FullName}' (Code: {member.MemberCode}) at Turnstile/Kiosk. Reason: Membership Expired or Inactive.",
                oldValues: "Expired/Inactive"
            );

            return new CheckInResultDto
            {
                Success = false,
                Message = "ACCESS DENIED: Membership Expired or Inactive.",
                Member = memberDto,
                CheckInTime = now,
                PlanName = "Expired",
                DaysRemaining = -1
            };
        }

        // Multi-branch roaming validation:
        // Check if member is scanning at a branch different from their registered HomeBranch
        if (member.BranchId.HasValue && currentBranchId > 0 && member.BranchId.Value != currentBranchId)
        {
            var tenant = await _db.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            bool tenantAllowsMultiBranch = tenant?.Subscription?.MultiBranchAccessEnabled ?? true;
            bool planAllowsMultiBranch = activeSub.MembershipPlan?.IsMultiBranchAllowed ?? true;

            if (!tenantAllowsMultiBranch || !planAllowsMultiBranch)
            {
                string homeBranchName = member.HomeBranch?.BranchName ?? "Home Branch";
                string denialReason = !tenantAllowsMultiBranch
                    ? $"Multi-Branch Roaming is disabled for this gym tenant. Member is registered at '{homeBranchName}'."
                    : $"Member plan '{activeSub.MembershipPlan?.PlanName}' does not permit cross-branch access. Home branch: '{homeBranchName}'.";

                var deniedLog = new MemberCheckIn
                {
                    TenantId = tenantId,
                    BranchId = currentBranchId,
                    MemberId = member.Id,
                    CheckInTime = now,
                    Status = CheckInStatus.Denied,
                    DenialReason = denialReason
                };
                deniedLog.AddDomainEvent(new TurnstileAccessDeniedEvent(member.Id, tenantId, currentBranchId, denialReason));
                _db.MemberCheckIns.Add(deniedLog);
                await _db.SaveChangesAsync();

                await _auditLog.LogAsync(
                    tenantId: tenantId,
                    branchId: currentBranchId,
                    userId: _currentUser.UserId,
                    username: _currentUser.Username ?? "FrontDesk",
                    userRole: _currentUser.Role?.ToString() ?? "Staff",
                    actionType: "ACCESS_DENIED",
                    module: "AccessControl",
                    entityName: "Member",
                    entityId: member.Id.ToString(),
                    details: $"Access Denied for '{member.FullName}' at Turnstile/Kiosk. Reason: {denialReason}",
                    oldValues: "Multi-Branch Prohibited"
                );

                return new CheckInResultDto
                {
                    Success = false,
                    Message = $"ACCESS DENIED: {denialReason}",
                    Member = memberDto,
                    CheckInTime = now,
                    PlanName = activeSub.MembershipPlan?.PlanName,
                    DaysRemaining = (int)(activeSub.EndDate.Date - now.Date).TotalDays
                };
            }
        }

        // We log successful check-in with current BranchId
        var checkInLog = new MemberCheckIn
        {
            TenantId = tenantId,
            BranchId = currentBranchId,
            MemberId = member.Id,
            CheckInTime = now,
            Status = CheckInStatus.Accepted
        };

        _db.MemberCheckIns.Add(checkInLog);
        await _db.SaveChangesAsync();

        int daysLeft = (int)(activeSub.EndDate.Date - now.Date).TotalDays;

        return new CheckInResultDto
        {
            Success = true,
            IsCheckOut = false,
            Message = $"Welcome {member.FullName}! Access Granted.",
            Member = memberDto,
            CheckInTime = now,
            PlanName = activeSub.MembershipPlan?.PlanName,
            DaysRemaining = daysLeft,
            WarningExpiringSoon = daysLeft <= 3
        };
    }

    public async Task<bool> ProcessCheckOutAsync(int checkInId)
    {
        var log = await _db.MemberCheckIns.FindAsync(checkInId);
        if (log == null || log.CheckOutTime.HasValue) return false;

        log.CheckOutTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<MemberDto>> GetDuePeriodAlertsAsync(int daysThreshold = 7)
    {
        var allMembers = await GetMembersAsync();
        return allMembers
            .Where(m => m.DaysRemaining <= daysThreshold)
            .OrderBy(m => m.DaysRemaining)
            .ToList();
    }

    public async Task<List<MemberCheckIn>> GetTodayCheckInsAsync(int? branchId = null)
    {
        var today = DateTime.UtcNow.Date;
        var query = _db.MemberCheckIns
            .Include(c => c.Member)
            .Include(c => c.Branch)
            .Where(c => c.CheckInTime.Date == today);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        return await query.OrderByDescending(c => c.CheckInTime).Take(50).ToListAsync();
    }

    public async Task<List<MemberPromotion>> GetPromotionsAsync()
    {
        var promos = await _db.MemberPromotions
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Seed default promotions if none exist
        if (promos.Count == 0)
        {
            var tenantId = _currentUser.TenantId ?? 1;
            var now = DateTime.UtcNow;

            var p1 = new MemberPromotion
            {
                TenantId = tenantId,
                PromotionTitle = "New Year Kickstart",
                Description = "Applicable for 3-Month and Annual VIP plans",
                DiscountPercent = 15,
                DiscountAmount = 0,
                ValidFrom = now.AddDays(-10),
                ValidTo = now.AddDays(14),
                IsActive = true
            };
            var p2 = new MemberPromotion
            {
                TenantId = tenantId,
                PromotionTitle = "Student Privilege",
                Description = "Valid with valid Student ID card",
                DiscountPercent = 0,
                DiscountAmount = 5000,
                ValidFrom = now.AddMonths(-1),
                ValidTo = now.AddMonths(1),
                IsActive = true
            };
            var p3 = new MemberPromotion
            {
                TenantId = tenantId,
                PromotionTitle = "Early Bird Holiday Pass",
                Description = "Limited seasonal registration discount",
                DiscountPercent = 20,
                DiscountAmount = 0,
                ValidFrom = now.AddDays(-30),
                ValidTo = now.AddDays(-2), // Already expired
                IsActive = true
            };

            _db.MemberPromotions.AddRange(p1, p2, p3);
            await _db.SaveChangesAsync();

            promos = await _db.MemberPromotions.OrderByDescending(p => p.CreatedAt).ToListAsync();
        }

        return promos;
    }

    public async Task<MemberPromotion> CreatePromotionAsync(string title, string? description, decimal discountPercent, decimal discountAmount, DateTime validFrom, DateTime validTo)
    {
        var tenantId = _currentUser.TenantId ?? 1;
        var promo = new MemberPromotion
        {
            TenantId = tenantId,
            PromotionTitle = title,
            Description = description,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            ValidFrom = validFrom,
            ValidTo = validTo,
            IsActive = true
        };

        _db.MemberPromotions.Add(promo);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: tenantId,
            branchId: null,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Admin",
            userRole: _currentUser.Role?.ToString() ?? "TenantAdmin",
            actionType: "CREATE",
            module: "Promotion",
            entityName: "MemberPromotion",
            entityId: promo.Id.ToString(),
            details: $"Created member discount promotion '{title}' (Expires: {validTo:yyyy-MM-dd HH:mm} UTC)."
        );

        return promo;
    }

    public async Task<MemberPromotion> UpdatePromotionAsync(int id, string title, string? description, decimal discountPercent, decimal discountAmount, DateTime validFrom, DateTime validTo, bool isActive)
    {
        var promo = await _db.MemberPromotions.FindAsync(id);
        if (promo == null) throw new InvalidOperationException($"Promotion with ID {id} not found.");

        promo.PromotionTitle = title;
        promo.Description = description;
        promo.DiscountPercent = discountPercent;
        promo.DiscountAmount = discountAmount;
        promo.ValidFrom = validFrom;
        promo.ValidTo = validTo;
        promo.IsActive = isActive;
        promo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: promo.TenantId,
            branchId: null,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Admin",
            userRole: _currentUser.Role?.ToString() ?? "TenantAdmin",
            actionType: "UPDATE",
            module: "Promotion",
            entityName: "MemberPromotion",
            entityId: promo.Id.ToString(),
            details: $"Updated member discount promotion '{title}'. Expires: {validTo:yyyy-MM-dd HH:mm} UTC, Active={isActive}."
        );

        return promo;
    }

    public async Task DeletePromotionAsync(int id)
    {
        var promo = await _db.MemberPromotions.FindAsync(id);
        if (promo == null) return;

        promo.IsDeleted = true;
        promo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: promo.TenantId,
            branchId: null,
            userId: _currentUser.UserId,
            username: _currentUser.Username ?? "Admin",
            userRole: _currentUser.Role?.ToString() ?? "TenantAdmin",
            actionType: "DELETE",
            module: "Promotion",
            entityName: "MemberPromotion",
            entityId: promo.Id.ToString(),
            details: $"Deleted member discount promotion '{promo.PromotionTitle}'."
        );
    }

    public async Task<List<MobileBranchGeofenceDto>> GetMobileGeofencesAsync()
    {
        int tenantId = _currentUser.TenantId ?? 1;
        var branches = await _db.Branches
            .Where(b => b.TenantId == tenantId && b.IsActive && b.Latitude != null && b.Longitude != null)
            .ToListAsync();

        return branches.Select(b => new MobileBranchGeofenceDto
        {
            BranchId = b.Id,
            BranchName = b.BranchName,
            BranchCode = b.BranchCode,
            Address = b.Address,
            Latitude = b.Latitude!.Value,
            Longitude = b.Longitude!.Value,
            RadiusMeters = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 50
        }).ToList();
    }

    public async Task<GeofenceCheckInResultDto> ProcessGeofenceCheckInAsync(string codeOrPhone, double userLat, double userLon, int? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(codeOrPhone))
        {
            return new GeofenceCheckInResultDto { Success = false, Message = "Member code or phone is required." };
        }

        var searchKey = codeOrPhone.Trim().ToLower();
        var member = await _db.Members
            .Include(m => m.HomeBranch)
            .Include(m => m.Subscriptions)
                .ThenInclude(s => s.MembershipPlan)
            .FirstOrDefaultAsync(m => m.MemberCode.ToLower() == searchKey || m.PhoneNumber == searchKey);

        if (member == null)
        {
            return new GeofenceCheckInResultDto { Success = false, Message = $"Member '{codeOrPhone}' not found." };
        }

        int tenantId = member.TenantId;
        var now = DateTime.UtcNow;

        // Find matching or nearest branch with geofence
        Branch? targetBranch = null;
        if (branchId.HasValue && branchId.Value > 0)
        {
            targetBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId.Value && b.IsActive);
        }

        if (targetBranch == null || targetBranch.Latitude == null || targetBranch.Longitude == null)
        {
            var activeBranches = await _db.Branches
                .Where(b => b.TenantId == tenantId && b.IsActive && b.Latitude != null && b.Longitude != null)
                .ToListAsync();

            if (!activeBranches.Any())
            {
                return new GeofenceCheckInResultDto
                {
                    Success = false,
                    Message = "No active gym branches configured with GPS coordinates."
                };
            }

            targetBranch = activeBranches
                .OrderBy(b => CalculateDistanceMeters(userLat, userLon, b.Latitude!.Value, b.Longitude!.Value))
                .First();
        }

        double distance = CalculateDistanceMeters(userLat, userLon, targetBranch.Latitude!.Value, targetBranch.Longitude!.Value);
        int allowedRadius = targetBranch.GeofenceRadiusMeters > 0 ? targetBranch.GeofenceRadiusMeters : 50;
        bool isInside = distance <= allowedRadius;

        var memberDto = await GetMemberByIdAsync(member.Id);

        if (!isInside)
        {
            return new GeofenceCheckInResultDto
            {
                Success = false,
                Message = $"Outside Gym Geofence: You are {distance:N0}m away from {targetBranch.BranchName} (allowed radius: {allowedRadius}m).",
                Member = memberDto,
                BranchId = targetBranch.Id,
                BranchName = targetBranch.BranchName,
                DistanceMeters = distance,
                AllowedRadiusMeters = allowedRadius,
                InsideGeofence = false,
                Timestamp = now
            };
        }

        // Inside geofence! Validate active subscription
        var activeSub = member.Subscriptions
            .Where(s => s.Status == MemberStatus.Active && s.EndDate >= now.Date)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        if (activeSub == null)
        {
            var deniedLog = new MemberCheckIn
            {
                TenantId = tenantId,
                BranchId = targetBranch.Id,
                MemberId = member.Id,
                CheckInTime = now,
                Status = CheckInStatus.Denied,
                DenialReason = "Membership Expired / Inactive (Geofence Auto Check-In)",
                Method = CheckInMethod.GeofenceAuto,
                Latitude = userLat,
                Longitude = userLon,
                DistanceMeters = distance
            };
            _db.MemberCheckIns.Add(deniedLog);
            await _db.SaveChangesAsync();

            return new GeofenceCheckInResultDto
            {
                Success = false,
                Message = "ACCESS DENIED: Membership Expired or Inactive.",
                Member = memberDto,
                BranchId = targetBranch.Id,
                BranchName = targetBranch.BranchName,
                DistanceMeters = distance,
                AllowedRadiusMeters = allowedRadius,
                InsideGeofence = true,
                Timestamp = now
            };
        }

        // Check if member already checked in today without checkout
        var today = now.Date;
        var openCheckIn = await _db.MemberCheckIns
            .Where(c => c.MemberId == member.Id && c.CheckInTime.Date == today && c.CheckOutTime == null)
            .OrderByDescending(c => c.CheckInTime)
            .FirstOrDefaultAsync();

        if (openCheckIn != null)
        {
            var duration = (int)(now - openCheckIn.CheckInTime).TotalMinutes;
            int hours = duration / 60;
            int mins = duration % 60;
            string formatted = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";

            return new GeofenceCheckInResultDto
            {
                Success = true,
                Message = $"Already checked in at {targetBranch.BranchName}. Workout in progress ({formatted}).",
                Member = memberDto,
                BranchId = targetBranch.Id,
                BranchName = targetBranch.BranchName,
                DistanceMeters = distance,
                AllowedRadiusMeters = allowedRadius,
                InsideGeofence = true,
                Timestamp = openCheckIn.CheckInTime,
                IsCheckOut = false,
                WorkoutDurationMinutes = duration,
                FormattedDuration = formatted
            };
        }

        // Multi-branch roaming check
        if (member.BranchId.HasValue && targetBranch.Id != member.BranchId.Value)
        {
            var tenant = await _db.Tenants.Include(t => t.Subscription).FirstOrDefaultAsync(t => t.Id == tenantId);
            bool tenantAllows = tenant?.Subscription?.MultiBranchAccessEnabled ?? true;
            bool planAllows = activeSub.MembershipPlan?.IsMultiBranchAllowed ?? true;

            if (!tenantAllows || !planAllows)
            {
                return new GeofenceCheckInResultDto
                {
                    Success = false,
                    Message = $"ACCESS DENIED: Multi-branch roaming not permitted for plan '{activeSub.MembershipPlan?.PlanName}'.",
                    Member = memberDto,
                    BranchId = targetBranch.Id,
                    BranchName = targetBranch.BranchName,
                    DistanceMeters = distance,
                    AllowedRadiusMeters = allowedRadius,
                    InsideGeofence = true,
                    Timestamp = now
                };
            }
        }

        // Create new Geofence Auto Check-In
        var checkIn = new MemberCheckIn
        {
            TenantId = tenantId,
            BranchId = targetBranch.Id,
            MemberId = member.Id,
            CheckInTime = now,
            Status = CheckInStatus.Accepted,
            Method = CheckInMethod.GeofenceAuto,
            Latitude = userLat,
            Longitude = userLon,
            DistanceMeters = distance
        };

        _db.MemberCheckIns.Add(checkIn);
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            tenantId: tenantId,
            branchId: targetBranch.Id,
            userId: null,
            username: member.FullName,
            userRole: "Member",
            actionType: "GEOFENCE_AUTO_CHECKIN",
            module: "AccessControl",
            entityName: "MemberCheckIn",
            entityId: checkIn.Id.ToString(),
            details: $"Auto Geofence Check-In for member '{member.FullName}' at '{targetBranch.BranchName}'. Distance from center: {distance:N0}m (GPS: {userLat:F4}, {userLon:F4}).",
            newValues: "Method: GeofenceAuto, Status: Accepted"
        );

        return new GeofenceCheckInResultDto
        {
            Success = true,
            Message = $"Welcome to {targetBranch.BranchName}! Auto Check-In successful via GPS Geofence.",
            Member = memberDto,
            BranchId = targetBranch.Id,
            BranchName = targetBranch.BranchName,
            DistanceMeters = distance,
            AllowedRadiusMeters = allowedRadius,
            InsideGeofence = true,
            Timestamp = now,
            IsCheckOut = false
        };
    }

    public async Task<GeofenceCheckInResultDto> ProcessGeofenceCheckOutAsync(string codeOrPhone, double userLat, double userLon, int? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(codeOrPhone))
        {
            return new GeofenceCheckInResultDto { Success = false, Message = "Member code or phone is required." };
        }

        var searchKey = codeOrPhone.Trim().ToLower();
        var member = await _db.Members
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.MemberCode.ToLower() == searchKey || m.PhoneNumber == searchKey);

        if (member == null)
        {
            return new GeofenceCheckInResultDto { Success = false, Message = $"Member '{codeOrPhone}' not found." };
        }

        var now = DateTime.UtcNow;
        var today = now.Date;

        var openCheckIn = await _db.MemberCheckIns
            .Include(c => c.Branch)
            .Where(c => c.MemberId == member.Id && c.CheckInTime.Date == today && c.CheckOutTime == null)
            .OrderByDescending(c => c.CheckInTime)
            .FirstOrDefaultAsync();

        var memberDto = await GetMemberByIdAsync(member.Id);

        if (openCheckIn == null)
        {
            return new GeofenceCheckInResultDto
            {
                Success = false,
                Message = "No active check-in session found to check out.",
                Member = memberDto,
                Timestamp = now
            };
        }

        // Close session & compute workout duration
        openCheckIn.CheckOutTime = now;
        await _db.SaveChangesAsync();

        int totalMins = (int)(now - openCheckIn.CheckInTime).TotalMinutes;
        int hours = totalMins / 60;
        int mins = totalMins % 60;
        string durationStr = hours > 0 ? $"{hours} hr {mins} mins" : $"{mins} mins";

        var branchName = openCheckIn.Branch?.BranchName ?? "Gym";

        await _auditLog.LogAsync(
            tenantId: member.TenantId,
            branchId: openCheckIn.BranchId,
            userId: null,
            username: member.FullName,
            userRole: "Member",
            actionType: "GEOFENCE_AUTO_CHECKOUT",
            module: "AccessControl",
            entityName: "MemberCheckIn",
            entityId: openCheckIn.Id.ToString(),
            details: $"Auto Geofence Check-Out for member '{member.FullName}' at '{branchName}'. Workout duration: {durationStr} ({totalMins} minutes).",
            newValues: $"CheckOutTime: {now:HH:mm}, Duration: {durationStr}"
        );

        return new GeofenceCheckInResultDto
        {
            Success = true,
            IsCheckOut = true,
            Message = $"Auto Checked Out from {branchName}! Workout duration: {durationStr}.",
            Member = memberDto,
            BranchId = openCheckIn.BranchId ?? 0,
            BranchName = branchName,
            Timestamp = now,
            WorkoutDurationMinutes = totalMins,
            FormattedDuration = durationStr
        };
    }

    public async Task<List<MemberCheckIn>> GetMemberCheckInsHistoryAsync(int memberId)
    {
        return await _db.MemberCheckIns
            .Include(c => c.Branch)
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.CheckInTime)
            .Take(30)
            .ToListAsync();
    }

    public async Task<MemberCheckIn?> GetActiveCheckInSessionAsync(int memberId)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.MemberCheckIns
            .Include(c => c.Branch)
            .Where(c => c.MemberId == memberId && c.CheckInTime.Date == today && c.CheckOutTime == null)
            .OrderByDescending(c => c.CheckInTime)
            .FirstOrDefaultAsync();
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 1);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
