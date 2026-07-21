using CallCadence.UI.Services;
using FluentAssertions;

namespace CallCadence.UnitTests.UI;

[TestFixture]
public sealed class UserSessionStateTests
{
    [Test]
    public void SignIn_WithTokenAndExpiry_PreservesAllValues()
    {
        // Arrange
        var session = new UserSessionState();
        const string email = "admin@example.com";
        const bool isAdmin = true;
        const string token = "sample-jwt-token";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        session.SignIn(email, isAdmin, token, expiresAt);

        // Assert
        session.IsAuthenticated.Should().BeTrue();
        session.Email.Should().Be(email);
        session.IsAdmin.Should().Be(isAdmin);
        session.Token.Should().Be(token);
        session.ExpiresAtUtc.Should().Be(expiresAt);
    }

    [Test]
    public void SignIn_WithTwoArgOverload_ClearsTokenAndExpiry()
    {
        // Arrange
        var session = new UserSessionState();
        const string email = "admin@example.com";
        const bool isAdmin = true;
        const string initialToken = "initial-jwt-token";
        var initialExpiry = DateTime.UtcNow.AddHours(1);

        // Set initial token
        session.SignIn(email, isAdmin, initialToken, initialExpiry);
        session.Token.Should().Be(initialToken);
        session.ExpiresAtUtc.Should().Be(initialExpiry);

        // Act - call 2-arg overload
        session.SignIn(email, isAdmin);

        // Assert - token and expiry are cleared
        session.IsAuthenticated.Should().BeTrue();
        session.Email.Should().Be(email);
        session.IsAdmin.Should().Be(isAdmin);
        session.Token.Should().BeNull();
        session.ExpiresAtUtc.Should().BeNull();
    }

    [Test]
    public void SignIn_FourArgOverloadPreservesExistingToken_WhenTokenNotProvided()
    {
        // Arrange
        var session = new UserSessionState();
        const string email = "admin@example.com";
        const bool isAdmin = true;
        const string existingToken = "existing-jwt-token";
        var existingExpiry = DateTime.UtcNow.AddHours(1);

        // Set initial session
        session.SignIn(email, isAdmin, existingToken, existingExpiry);

        // Act - re-affirm session by passing existing token/expiry back in
        session.SignIn(email, isAdmin, session.Token, session.ExpiresAtUtc);

        // Assert - token and expiry are preserved
        session.IsAuthenticated.Should().BeTrue();
        session.Email.Should().Be(email);
        session.IsAdmin.Should().Be(isAdmin);
        session.Token.Should().Be(existingToken);
        session.ExpiresAtUtc.Should().Be(existingExpiry);
    }

    [Test]
    public void SignOut_ClearsAllSessionData()
    {
        // Arrange
        var session = new UserSessionState();
        session.SignIn("admin@example.com", true, "token", DateTime.UtcNow.AddHours(1));

        // Act
        session.SignOut();

        // Assert
        session.IsAuthenticated.Should().BeFalse();
        session.Email.Should().BeNull();
        session.IsAdmin.Should().BeFalse();
        session.Token.Should().BeNull();
        session.ExpiresAtUtc.Should().BeNull();
    }
}
