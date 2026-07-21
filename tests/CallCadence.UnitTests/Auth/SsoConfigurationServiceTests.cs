using CallCadence.API.Auth;
using CallCadence.Infrastructure.ApiCall;
using CallCadence.Models.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.UnitTests.Auth;

[TestFixture]
public sealed class SsoConfigurationServiceTests
{
    private static CallCadenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CallCadenceDbContext>()
            .UseInMemoryDatabase($"SsoTest_{Guid.NewGuid()}")
            .Options;
        return new CallCadenceDbContext(options);
    }

    // ── Environment-variable override tests ──────────────────────────────────

    [Test]
    public void IsOverriddenByEnvironment_WhenEnvConfigsProvided_ReturnsTrue()
    {
        var envConfigs = new List<SsoConfiguration>
        {
            new() { Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
                Authority = "https://login.microsoftonline.com/tenant/v2.0",
                ClientId = "c1", ClientSecret = "s1", UpdatedAt = DateTime.UtcNow }
        }.AsReadOnly();

        var service = new SsoConfigurationService(null, envConfigs);

        service.IsOverriddenByEnvironment.Should().BeTrue();
    }

    [Test]
    public void IsOverriddenByEnvironment_WhenNoEnvConfigs_ReturnsFalse()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SsoConfigurationService(db);

        service.IsOverriddenByEnvironment.Should().BeFalse();
    }

    [Test]
    public async Task GetAllAsync_WhenEnvOverride_ReturnsEnvConfigs()
    {
        var envConfigs = new List<SsoConfiguration>
        {
            new() { Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
                Authority = "https://example.com", ClientId = "c1", ClientSecret = "s1",
                UpdatedAt = DateTime.UtcNow },
            new() { Name = "Google", SchemeName = "oidc-google", IsEnabled = false,
                Authority = "https://accounts.google.com", ClientId = "c2", ClientSecret = "s2",
                UpdatedAt = DateTime.UtcNow }
        }.AsReadOnly();

        var service = new SsoConfigurationService(null, envConfigs);

        var result = await service.GetAllAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Azure AD");
        result[0].ClientSecret.Should().BeNull();  // Never returned
        result[0].HasClientSecret.Should().BeTrue();
        result[1].Name.Should().Be("Google");
    }

    [Test]
    public async Task GetAllAsync_WhenDbBacked_ReturnsDbRows()
    {
        using var db = CreateInMemoryDbContext();
        db.SsoConfigurations.Add(new SsoConfiguration
        {
            Name = "My IdP", SchemeName = "oidc-my-idp", IsEnabled = true,
            Authority = "https://idp.example.com", ClientId = "c1", ClientSecret = "s1",
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new SsoConfigurationService(db);

        var result = await service.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("My IdP");
    }

    [Test]
    public async Task GetBySchemeNameAsync_WhenEnvOverride_FindsByScheme()
    {
        var envConfigs = new List<SsoConfiguration>
        {
            new() { Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
                Authority = "https://example.com", ClientId = "c1", ClientSecret = "s1",
                UpdatedAt = DateTime.UtcNow }
        }.AsReadOnly();

        var service = new SsoConfigurationService(null, envConfigs);

        var result = await service.GetBySchemeNameAsync("oidc-azure-ad");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Azure AD");
    }

    [Test]
    public async Task GetBySchemeNameAsync_WhenSchemeNotFound_ReturnsNull()
    {
        var envConfigs = new List<SsoConfiguration>
        {
            new() { Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
                Authority = "https://example.com", ClientId = "c1", ClientSecret = "s1",
                UpdatedAt = DateTime.UtcNow }
        }.AsReadOnly();

        var service = new SsoConfigurationService(null, envConfigs);

        var result = await service.GetBySchemeNameAsync("oidc-unknown");
        result.Should().BeNull();
    }

    [Test]
    public void UpsertAsync_WhenEnvOverride_ThrowsInvalidOperationException()
    {
        var envConfigs = new List<SsoConfiguration>().AsReadOnly();
        var service = new SsoConfigurationService(null, envConfigs);

        var act = async () => await service.UpsertAsync(new UpsertSsoConfigurationRequest
        {
            Name = "Test", Authority = "https://example.com", ClientId = "c1"
        });

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*environment variable*");
    }

    [Test]
    public void DeleteAsync_WhenEnvOverride_ThrowsInvalidOperationException()
    {
        var envConfigs = new List<SsoConfiguration>().AsReadOnly();
        var service = new SsoConfigurationService(null, envConfigs);

        var act = async () => await service.DeleteAsync("oidc-test");

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*environment variable*");
    }

    // ── DB-backed tests ───────────────────────────────────────────────────────

    [Test]
    public async Task UpsertAsync_WhenNew_InsertsRow()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SsoConfigurationService(db);

        var result = await service.UpsertAsync(new UpsertSsoConfigurationRequest
        {
            Name = "Azure AD",
            IsEnabled = true,
            Authority = "https://login.microsoftonline.com/tenant/v2.0",
            ClientId = "c1",
            ClientSecret = "s1",
            CallbackPath = "/signin-oidc-azure"
        });

        result.Name.Should().Be("Azure AD");
        result.SchemeName.Should().Be("oidc-azure-ad");
        result.IsEnabled.Should().BeTrue();
        result.ClientSecret.Should().BeNull();
        result.HasClientSecret.Should().BeTrue();

        db.SsoConfigurations.Should().HaveCount(1);
    }

    [Test]
    public async Task UpsertAsync_WhenExistingScheme_UpdatesRow()
    {
        using var db = CreateInMemoryDbContext();
        db.SsoConfigurations.Add(new SsoConfiguration
        {
            Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
            Authority = "https://old.example.com", ClientId = "old-client",
            ClientSecret = "old-secret", CallbackPath = "/old",
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = new SsoConfigurationService(db);

        await service.UpsertAsync(new UpsertSsoConfigurationRequest
        {
            Name = "Azure AD",
            IsEnabled = false,
            Authority = "https://new.example.com",
            ClientId = "new-client",
            CallbackPath = "/new"
            // ClientSecret omitted — should keep existing
        });

        var row = db.SsoConfigurations.Single();
        row.IsEnabled.Should().BeFalse();
        row.Authority.Should().Be("https://new.example.com");
        row.ClientId.Should().Be("new-client");
        row.ClientSecret.Should().Be("old-secret");   // preserved
        row.CallbackPath.Should().Be("/new");
    }

    [Test]
    public async Task DeleteAsync_WhenExists_RemovesRow()
    {
        using var db = CreateInMemoryDbContext();
        db.SsoConfigurations.Add(new SsoConfiguration
        {
            Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
            Authority = "https://example.com", ClientId = "c1",
            ClientSecret = "s1", UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new SsoConfigurationService(db);
        await service.DeleteAsync("oidc-azure-ad");

        db.SsoConfigurations.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_WhenNotExists_DoesNotThrow()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SsoConfigurationService(db);

        var act = async () => await service.DeleteAsync("oidc-nonexistent");

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task IsEnabledAsync_WhenEnvOverrideHasEnabledProvider_ReturnsTrue()
    {
        var envConfigs = new List<SsoConfiguration>
        {
            new() { Name = "Azure AD", SchemeName = "oidc-azure-ad", IsEnabled = true,
                Authority = "https://example.com", ClientId = "c1", ClientSecret = "s1",
                UpdatedAt = DateTime.UtcNow }
        }.AsReadOnly();

        var service = new SsoConfigurationService(null, envConfigs);

        var result = await service.IsEnabledAsync();
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsEnabledAsync_WhenNoEnabledProviders_ReturnsFalse()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SsoConfigurationService(db);

        var result = await service.IsEnabledAsync();
        result.Should().BeFalse();
    }
}
