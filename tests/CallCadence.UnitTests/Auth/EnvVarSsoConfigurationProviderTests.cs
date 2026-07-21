using CallCadence.API.Auth;
using FluentAssertions;

namespace CallCadence.UnitTests.Auth;

[TestFixture]
public sealed class EnvVarSsoConfigurationProviderTests
{
    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable(EnvVarSsoConfigurationProvider.EnvironmentVariableName, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(EnvVarSsoConfigurationProvider.EnvironmentVariableName, null);
    }

    [Test]
    public void TryLoad_WhenEnvVarIsAbsent_ReturnsNull()
    {
        var result = EnvVarSsoConfigurationProvider.TryLoad();

        result.Should().BeNull();
    }

    [Test]
    public void TryLoad_WhenEnvVarIsEmpty_ReturnsNull()
    {
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, "   ");

        var result = EnvVarSsoConfigurationProvider.TryLoad();

        result.Should().BeNull();
    }

    [Test]
    public void TryLoad_WhenEnvVarIsValidJson_ReturnsConfigurations()
    {
        var json = """
            [
              {
                "name": "Azure AD",
                "isEnabled": true,
                "authority": "https://login.microsoftonline.com/tenant/v2.0",
                "clientId": "client-1",
                "clientSecret": "secret-1",
                "callbackPath": "/signin-oidc-azure"
              },
              {
                "name": "Google",
                "isEnabled": false,
                "authority": "https://accounts.google.com",
                "clientId": "client-2",
                "clientSecret": "secret-2",
                "callbackPath": "/signin-oidc-google"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var result = EnvVarSsoConfigurationProvider.TryLoad();

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);

        result[0].Name.Should().Be("Azure AD");
        result[0].SchemeName.Should().Be("oidc-azure-ad");
        result[0].IsEnabled.Should().BeTrue();
        result[0].Authority.Should().Be("https://login.microsoftonline.com/tenant/v2.0");
        result[0].ClientId.Should().Be("client-1");
        result[0].ClientSecret.Should().Be("secret-1");
        result[0].CallbackPath.Should().Be("/signin-oidc-azure");

        result[1].Name.Should().Be("Google");
        result[1].SchemeName.Should().Be("oidc-google");
        result[1].IsEnabled.Should().BeFalse();
    }

    [Test]
    public void TryLoad_WhenSchemeNameIsProvided_UsesProvidedSchemeName()
    {
        var json = """
            [
              {
                "name": "My IdP",
                "schemeName": "custom-scheme",
                "authority": "https://idp.example.com",
                "clientId": "client-1",
                "clientSecret": "secret-1"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var result = EnvVarSsoConfigurationProvider.TryLoad();

        result!.Should().HaveCount(1);
        result![0].SchemeName.Should().Be("custom-scheme");
    }

    [Test]
    public void TryLoad_WhenEnvVarContainsInvalidJson_ThrowsInvalidOperationException()
    {
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, "not-json");

        var act = () => EnvVarSsoConfigurationProvider.TryLoad();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid JSON*");
    }

    [Test]
    public void TryLoad_WhenEntryIsMissingName_ThrowsInvalidOperationException()
    {
        var json = """
            [
              {
                "authority": "https://idp.example.com",
                "clientId": "client-1",
                "clientSecret": "secret-1"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var act = () => EnvVarSsoConfigurationProvider.TryLoad();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing 'name'*");
    }

    [Test]
    public void TryLoad_WhenEntryIsMissingClientId_ThrowsInvalidOperationException()
    {
        var json = """
            [
              {
                "name": "My IdP",
                "authority": "https://idp.example.com",
                "clientSecret": "secret-1"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var act = () => EnvVarSsoConfigurationProvider.TryLoad();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing 'clientId'*");
    }

    [Test]
    public void TryLoad_WhenEntryHasNeitherAuthorityNorMetadataAddress_ThrowsInvalidOperationException()
    {
        var json = """
            [
              {
                "name": "My IdP",
                "clientId": "client-1",
                "clientSecret": "secret-1"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var act = () => EnvVarSsoConfigurationProvider.TryLoad();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'authority' or 'metadataAddress'*");
    }

    [Test]
    public void TryLoad_WhenEntryHasMetadataAddressOnly_Succeeds()
    {
        var json = """
            [
              {
                "name": "My IdP",
                "metadataAddress": "https://idp.example.com/.well-known/openid-configuration",
                "clientId": "client-1",
                "clientSecret": "secret-1"
              }
            ]
            """;
        Environment.SetEnvironmentVariable(
            EnvVarSsoConfigurationProvider.EnvironmentVariableName, json);

        var result = EnvVarSsoConfigurationProvider.TryLoad();

        result.Should().HaveCount(1);
        result![0].MetadataAddress.Should().Be("https://idp.example.com/.well-known/openid-configuration");
        result[0].Authority.Should().BeNull();
    }

    [Test]
    [TestCase("Azure AD", "oidc-azure-ad")]
    [TestCase("Google", "oidc-google")]
    [TestCase("My Company IdP", "oidc-my-company-idp")]
    [TestCase("  Spaces  ", "oidc-spaces")]
    [TestCase("Special#Chars!", "oidc-specialchars")]
    public void DeriveSchemeNameFromProviderName_GeneratesExpectedSlug(string name, string expected)
    {
        var result = EnvVarSsoConfigurationProvider.DeriveSchemeNameFromProviderName(name);

        result.Should().Be(expected);
    }
}
