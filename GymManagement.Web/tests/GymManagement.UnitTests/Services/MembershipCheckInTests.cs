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

public class MembershipCheckInTests
{
    private GymDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.IsSuperAdmin).Returns(true);
        mockCurrentUser.Setup(u => u.TenantId).Returns(1);
        mockCurrentUser.Setup(u => u.BranchId).Returns(1);

        return new GymDbContext(options, mockCurrentUser.Object);
    }

    [Fact]
    public async Task ProcessCheckIn_ShouldDenyAccess_WhenMemberSubscriptionIsExpired()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(1);
        mockCurrentUser.Setup(u => u.BranchId).Returns(1);

        var mockAuditLog = new Mock<IAuditLogService>();
        var mockCache = new Mock<ICacheService>();

        var membershipService = new MembershipService(db, mockCurrentUser.Object, mockAuditLog.Object, mockCache.Object);

        var expiredMember = new Member
        {
            Id = 1,
            TenantId = 1,
            BranchId = 1,
            MemberCode = "MBR-99901",
            FullName = "Zaw Min",
            PhoneNumber = "0944001122",
            Status = MemberStatus.Expired
        };
        // Add an expired subscription (ended 5 days ago)
        expiredMember.Subscriptions.Add(new MemberSubscription
        {
            TenantId = 1,
            BranchId = 1,
            StartDate = DateTime.UtcNow.AddDays(-35),
            EndDate = DateTime.UtcNow.AddDays(-5),
            Status = MemberStatus.Expired
        });

        db.Members.Add(expiredMember);
        await db.SaveChangesAsync();

        // Act
        var result = await membershipService.ProcessCheckInAsync("MBR-99901", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ACCESS DENIED");
    }

    [Fact]
    public async Task ProcessCheckIn_ShouldAutoCheckOut_WhenMemberScansTwiceInSameDay()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(1);
        mockCurrentUser.Setup(u => u.BranchId).Returns(1);

        var mockAuditLog = new Mock<IAuditLogService>();
        var mockCache = new Mock<ICacheService>();

        var membershipService = new MembershipService(db, mockCurrentUser.Object, mockAuditLog.Object, mockCache.Object);

        var activeMember = new Member
        {
            Id = 2,
            TenantId = 1,
            BranchId = 1,
            MemberCode = "MBR-10002",
            FullName = "Kyaw Thu",
            PhoneNumber = "0955001122",
            Status = MemberStatus.Active
        };
        activeMember.Subscriptions.Add(new MemberSubscription
        {
            TenantId = 1,
            BranchId = 1,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(25),
            Status = MemberStatus.Active
        });

        db.Members.Add(activeMember);
        await db.SaveChangesAsync();

        // Act 1: First scan -> Check-In
        var checkInResult = await membershipService.ProcessCheckInAsync("MBR-10002", 1);
        checkInResult.Success.Should().BeTrue();
        checkInResult.IsCheckOut.Should().BeFalse();

        // Act 2: Second scan within same day -> Dual Scan Auto Check-Out
        var checkOutResult = await membershipService.ProcessCheckInAsync("MBR-10002", 1);

        // Assert
        checkOutResult.Success.Should().BeTrue();
        checkOutResult.IsCheckOut.Should().BeTrue();
        checkOutResult.Message.Should().Contain("Checked Out");
    }
}
