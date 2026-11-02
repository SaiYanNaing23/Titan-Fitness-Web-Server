using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Common;
using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Data;

public class GymDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDomainEventDispatcher? _dispatcher;

    public GymDbContext(
        DbContextOptions<GymDbContext> options, 
        ICurrentUserService currentUserService,
        IDomainEventDispatcher? dispatcher = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _dispatcher = dispatcher;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantMonthlyInvoice> TenantMonthlyInvoices => Set<TenantMonthlyInvoice>();

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<Member> Members => Set<Member>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MemberPromotion> MemberPromotions => Set<MemberPromotion>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<MemberCheckIn> MemberCheckIns => Set<MemberCheckIn>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<ProductPromotion> ProductPromotions => Set<ProductPromotion>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockRequest> StockRequests => Set<StockRequest>();
    public DbSet<StockRequestItem> StockRequestItems => Set<StockRequestItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FinancialAdjustmentRequest> FinancialAdjustmentRequests => Set<FinancialAdjustmentRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-tenancy & Soft Delete Global Query Filters
        modelBuilder.Entity<TenantSubscription>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<TenantMonthlyInvoice>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<Branch>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<StaffUser>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<Holiday>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<Member>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<MembershipPlan>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<MemberPromotion>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<MemberSubscription>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<MemberCheckIn>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<ProductCategory>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<ProductPriceHistory>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<ProductPromotion>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<StockBalance>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<StockTransaction>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<StockRequest>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<SalesOrder>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));
        modelBuilder.Entity<FinancialAdjustmentRequest>().HasQueryFilter(e => !e.IsDeleted && (_currentUserService.IsSuperAdmin || _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId));

        // High-Performance Database Indexes
        modelBuilder.Entity<Member>().HasIndex(m => new { m.TenantId, m.MemberCode });
        modelBuilder.Entity<Member>().HasIndex(m => new { m.TenantId, m.PhoneNumber });
        modelBuilder.Entity<Member>().HasIndex(m => new { m.TenantId, m.Status });
        modelBuilder.Entity<MemberSubscription>().HasIndex(s => new { s.TenantId, s.MemberId, s.Status, s.EndDate });
        modelBuilder.Entity<MemberCheckIn>().HasIndex(c => new { c.TenantId, c.MemberId, c.CheckInTime });
        modelBuilder.Entity<Product>().HasIndex(p => new { p.TenantId, p.Barcode });
        modelBuilder.Entity<SalesOrder>().HasIndex(o => new { o.TenantId, o.InvoiceNumber });
        modelBuilder.Entity<FinancialAdjustmentRequest>().HasIndex(f => new { f.TenantId, f.Status });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.Timestamp });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.ActionType });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.Module });

        // Configure Decimal Precisions
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // Configure Relationships
        modelBuilder.Entity<StockRequest>()
            .HasOne(r => r.RequestingBranch)
            .WithMany()
            .HasForeignKey(r => r.RequestingBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockRequest>()
            .HasOne(r => r.FulfillingBranch)
            .WithMany()
            .HasForeignKey(r => r.FulfillingBranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>().ToList();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Soft Delete Interceptor
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }

        var entitiesWithEvents = entries
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events post-save
        if (_dispatcher != null && entitiesWithEvents.Any())
        {
            await _dispatcher.DispatchEventsAsync(entitiesWithEvents, cancellationToken);
        }

        return result;
    }
}
