using FluentAssertions;
using GymManagement.Infrastructure.Services;

namespace GymManagement.UnitTests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ShouldProduceVerifiableHash_AndRejectIncorrectPassword()
    {
        // Arrange
        var hasher = new PasswordHasherService();
        var rawPassword = "StrongGymAdmin@2026!";
        var wrongPassword = "WrongPassword123";

        // Act
        var hash = hasher.HashPassword(rawPassword);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Contain(".");

        // Verification tests
        hasher.VerifyPassword(rawPassword, hash).Should().BeTrue();
        hasher.VerifyPassword(wrongPassword, hash).Should().BeFalse();
    }
}
