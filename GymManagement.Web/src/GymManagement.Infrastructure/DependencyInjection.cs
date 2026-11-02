using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Events;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Database setup:
        // If MySQL connection string is provided and configured to use MySQL, use Pomelo.
        // Otherwise default to local SQLite / Embedded for instant zero-configuration demo.
        var dbProvider = configuration["DatabaseProvider"] ?? "Sqlite";

        if (dbProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(connectionString))
        {
            var serverVersion = ServerVersion.AutoDetect(connectionString);
            services.AddDbContext<GymDbContext>(options =>
                options.UseMySql(connectionString, serverVersion, b => 
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)
                     .MigrationsAssembly(typeof(GymDbContext).Assembly.FullName)));
        }
        else
        {
            var sqliteConn = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=gym_management.db";
            services.AddDbContext<GymDbContext>(options =>
                options.UseSqlite(sqliteConn, b => b.MigrationsAssembly(typeof(GymDbContext).Assembly.FullName)));
        }

        // Memory Caching & Performance
        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();

        // Security & Cryptography
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();

        // Domain Event Dispatcher & Handlers
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<LowStockAlertEvent>, LowStockAlertEventHandler>();

        // Register Current User Scoped Context
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Register Application Services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFinancialService, FinancialService>();

        // Real-Time Cross-Session Payment Notification Broadcaster
        services.AddSingleton<IPaymentNotificationService, PaymentNotificationService>();

        return services;
    }
}
