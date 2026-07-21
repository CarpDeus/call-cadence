using CallCadence.API.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace CallCadence.UnitTests.Auth;

[TestFixture]
public sealed class ConfigurationExtensionsTests
{
    [Test]
    public void GetAllowedUiReturnUrls_WhenArrayConfigured_ReturnsEntries()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AllowedUiReturnUrls:0"] = "https://app.example.com",
            ["AllowedUiReturnUrls:1"] = "https://admin.example.com/"
        });

        var result = configuration.GetAllowedUiReturnUrls();

        result.Should().BeEquivalentTo("https://app.example.com", "https://admin.example.com");
    }

    [Test]
    public void GetAllowedUiReturnUrls_WhenDelimitedString_ParsesEntries()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AllowedUiReturnUrls"] = "https://app.example.com, https://admin.example.com; https://ops.example.com/"
        });

        var result = configuration.GetAllowedUiReturnUrls();

        result.Should().BeEquivalentTo(
            "https://app.example.com",
            "https://admin.example.com",
            "https://ops.example.com");
    }

    [Test]
    public void GetAllowedUiReturnUrls_WhenArrayPresent_TakesPrecedenceOverDelimitedString()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AllowedUiReturnUrls:0"] = "https://array.example.com",
            ["AllowedUiReturnUrls"] = "https://string.example.com"
        });

        var result = configuration.GetAllowedUiReturnUrls();

        result.Should().ContainSingle().Which.Should().Be("https://array.example.com");
    }

    [Test]
    public void GetAllowedUiReturnUrls_WhenNotConfigured_ReturnsEmpty()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var result = configuration.GetAllowedUiReturnUrls();

        result.Should().BeEmpty();
    }

    [Test]
    public void GetAllowedUiReturnUrls_DeduplicatesCaseInsensitively()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AllowedUiReturnUrls"] = "https://app.example.com,https://APP.example.com/"
        });

        var result = configuration.GetAllowedUiReturnUrls();

        result.Should().ContainSingle();
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
