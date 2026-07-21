using System.Net;
using System.Text;
using FluentAssertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using CallCadence.Infrastructure.ApiCall;
using CallCadence.IntegrationTests.TestAuthentication;

namespace CallCadence.IntegrationTests.Hubs;

[TestFixture]
public sealed class GenericHubTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        var databaseName = $"TestDatabase_{Guid.NewGuid()}";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.AddTestAuthentication();

                    var descriptorsToRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<CallCadenceDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(CallCadenceDbContext)).ToList();

                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<CallCadenceDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(databaseName);
                    });

                    services.RemoveAll(typeof(IGlobalConfiguration));
                    services.RemoveAll(typeof(IBackgroundJobClient));
                    services.RemoveAll(typeof(IRecurringJobManager));
                    services.RemoveAll(typeof(JobStorage));

                    services.AddHangfire(config => config
                        .UseMemoryStorage());
                    services.AddSingleton<IRecurringJobManager>(_ =>
                    {
                        var storage = new MemoryStorage();
                        JobStorage.Current = storage;
                        return new RecurringJobManager(storage);
                    });
                });
            });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Negotiate_ShouldReturnOk()
    {
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/hubs/generic/negotiate?negotiateVersion=1", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
