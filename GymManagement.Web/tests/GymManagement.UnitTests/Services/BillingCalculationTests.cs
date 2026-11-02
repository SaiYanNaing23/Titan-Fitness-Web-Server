using FluentAssertions;
using GymManagement.Application.Common.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GymManagement.UnitTests.Services;

public class BillingCalculationTests
{
    private GymDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsSuperAdmin).Returns(true);
        mockCurrentUser.Setup(u => u.TenantId).Returns((int?)null);

        return new GymDbContext(options, mockCurrentUser.Object);
    }

    [Fact]
    public async Task CalculateMonthlyInvoices_ShouldPreserveHistoricalLockedRates_WhenTenantRateChangesLater()
    {
        // Arrange: Setup Tenant with base rate 1,500 MMK
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsSuperAdmin).Returns(true);
        mockCurrentUser.Setup(u => u.TenantId).Returns((int?)null);

        var mockAuditLog = new Mock<IAuditLogService>();
        var tenantService = new TenantService(db, mockCurrentUser.Object, mockAuditLog.Object);

        var tenant = new Tenant
        {
            Id = 1,
            CompanyName = "Apex Fitness",
            Code = "APEX",
            RatePerActiveMember = 1500m,
            Status = TenantStatus.Active
        };
        db.Tenants.Add(tenant);

        // Add 10 active members with active subscriptions for Year 2026 Month 8
        for (int i = 1; i <= 10; i++)
        {
            var member = new Member
            {
                Id = i,
                TenantId = tenant.Id,
                MemberCode = $"MBR-00{i}",
                FullName = $"Member {i}",
                PhoneNumber = $"09112233{i}",
                Status = MemberStatus.Active
            };
            member.Subscriptions.Add(new MemberSubscription
            {
                TenantId = tenant.Id,
                StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = MemberStatus.Active
            });
            db.Members.Add(member);
        }
        await db.SaveChangesAsync();

        // Act 1: Calculate billing for August 2026 (Month 8) -> Rate should be 1,500 MMK, Total: 15,000 MMK
        var augInvoices = await tenantService.CalculateMonthlyInvoicesAsync(2026, 8);
        var augInvoice = augInvoices.Should().ContainSingle().Subject;
        augInvoice.RatePerMember.Should().Be(1500m);
        augInvoice.TotalAmount.Should().Be(15000m);

        // Act 2: Super Admin updates tenant's rate to 2,500 MMK for future months
        tenant.RatePerActiveMember = 2500m;
        await db.SaveChangesAsync();

        // Re-calculate August 2026 -> Historical rate MUST be preserved at 1,500 MMK!
        var recheckedAug = await tenantService.CalculateMonthlyInvoicesAsync(2026, 8);
        var historicalInvoice = recheckedAug.Should().ContainSingle().Subject;

        // Assert: Historical rate must remain locked at 1,500 MMK
        historicalInvoice.RatePerMember.Should().Be(1500m);
        historicalInvoice.TotalAmount.Should().Be(15000m);
    }
}
