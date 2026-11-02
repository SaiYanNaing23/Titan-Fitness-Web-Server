using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GymManagement.Application.DTOs;
using GymManagement.Application.Services;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymManagement.Infrastructure.Services;

public class PaymentNotificationService : IPaymentNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private readonly List<PaymentNotificationDto> _notifications = new();
    private bool _isInitialized = false;
    private const int MaxNotifications = 100;

    public event Func<PaymentNotificationDto, Task>? OnPaymentReceived;

    public PaymentNotificationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task NotifyPaymentAsync(PaymentNotificationDto notification)
    {
        if (notification == null) return;

        EnsureInitialized();

        lock (_lock)
        {
            _notifications.Insert(0, notification);
            if (_notifications.Count > MaxNotifications)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
        }

        if (OnPaymentReceived != null)
        {
            var handlers = OnPaymentReceived.GetInvocationList();
            foreach (Func<PaymentNotificationDto, Task> handler in handlers)
            {
                try
                {
                    await handler(notification);
                }
                catch
                {
                    // Ignore transient delegate errors so other listeners continue receiving events
                }
            }
        }
    }

    public List<PaymentNotificationDto> GetRecentNotifications(int? tenantId = null, int limit = 20)
    {
        EnsureInitialized();
        lock (_lock)
        {
            var query = _notifications.AsEnumerable();
            if (tenantId.HasValue && tenantId.Value > 0)
            {
                query = query.Where(n => n.TenantId == tenantId.Value);
            }
            return query.Take(limit).ToList();
        }
    }

    public int GetUnreadCount(int? tenantId = null)
    {
        EnsureInitialized();
        lock (_lock)
        {
            var query = _notifications.Where(n => !n.IsRead);
            if (tenantId.HasValue && tenantId.Value > 0)
            {
                query = query.Where(n => n.TenantId == tenantId.Value);
            }
            return query.Count();
        }
    }

    public void MarkAsRead(string notificationId)
    {
        EnsureInitialized();
        lock (_lock)
        {
            var notif = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notif != null)
            {
                notif.IsRead = true;
            }
        }
    }

    public void MarkAllAsRead(int? tenantId = null)
    {
        EnsureInitialized();
        lock (_lock)
        {
            var query = _notifications.AsEnumerable();
            if (tenantId.HasValue && tenantId.Value > 0)
            {
                query = query.Where(n => n.TenantId == tenantId.Value);
            }
            foreach (var n in query)
            {
                n.IsRead = true;
            }
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        lock (_lock)
        {
            if (_isInitialized) return;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
                var today = DateTime.UtcNow.Date;

                // Load subscriptions
                var subs = db.MemberSubscriptions
                    .Include(s => s.Branch)
                    .Include(s => s.Member)
                    .Include(s => s.MembershipPlan)
                    .Where(s => !s.IsVoided && (s.CreatedAt >= today || s.StartDate >= today))
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(30)
                    .ToList();

                foreach (var s in subs)
                {
                    _notifications.Add(new PaymentNotificationDto
                    {
                        TenantId = s.TenantId,
                        BranchId = s.BranchId,
                        BranchName = s.Branch?.BranchName ?? "Main Branch",
                        TransactionType = "MEMBER_FEE",
                        ReferenceNumber = s.Member?.MemberCode ?? $"SUB-{s.Id}",
                        CustomerName = s.Member?.FullName ?? "Member",
                        ItemDescription = $"Membership Fee ({s.MembershipPlan?.PlanName ?? "Plan"})",
                        Amount = s.FinalAmountPaid,
                        PaymentMethod = s.PaymentMethod.ToString(),
                        CashierOrStaff = "Front Desk Staff",
                        Timestamp = s.CreatedAt,
                        IsRead = true
                    });
                }

                // Load sales orders
                var sales = db.SalesOrders
                    .Include(s => s.Branch)
                    .Include(s => s.Member)
                    .Include(s => s.Items)
                    .Where(s => !s.IsVoided && s.CreatedAt >= today)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(30)
                    .ToList();

                foreach (var s in sales)
                {
                    _notifications.Add(new PaymentNotificationDto
                    {
                        TenantId = s.TenantId,
                        BranchId = s.BranchId,
                        BranchName = s.Branch?.BranchName ?? "Main Branch",
                        TransactionType = "STOCK_SALE",
                        ReferenceNumber = s.InvoiceNumber,
                        CustomerName = s.Member != null ? s.Member.FullName : "Walk-in Customer",
                        ItemDescription = $"POS Retail Sale ({s.Items.Count} items)",
                        Amount = s.TotalAmount,
                        PaymentMethod = s.PaymentMethod.ToString(),
                        CashierOrStaff = s.CashierName ?? "Cashier",
                        Timestamp = s.CreatedAt,
                        IsRead = true
                    });
                }

                var sorted = _notifications.OrderByDescending(n => n.Timestamp).Take(MaxNotifications).ToList();
                _notifications.Clear();
                _notifications.AddRange(sorted);
            }
            catch
            {
                // Silently fallback if DB is still bootstrapping
            }
            finally
            {
                _isInitialized = true;
            }
        }
    }
}

public class NullPaymentNotificationService : IPaymentNotificationService
{
    public event Func<PaymentNotificationDto, Task>? OnPaymentReceived { add { } remove { } }
    public Task NotifyPaymentAsync(PaymentNotificationDto notification) => Task.CompletedTask;
    public List<PaymentNotificationDto> GetRecentNotifications(int? tenantId = null, int limit = 20) => new();
    public int GetUnreadCount(int? tenantId = null) => 0;
    public void MarkAsRead(string notificationId) {}
    public void MarkAllAsRead(int? tenantId = null) {}
}

