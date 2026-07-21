using System.Net;
using System.Net.Http.Json;
using CallCadence.Application.Tags;
using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using CallCadence.IntegrationTests.TestAuthentication;

namespace CallCadence.IntegrationTests.Controllers;

[TestFixture]
public sealed class TagsControllerTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        var databaseName = $"TagTestDatabase_{Guid.NewGuid()}";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.AddTestAuthentication();

                    var descriptorsToRemove = services.Where(descriptor =>
                        descriptor.ServiceType == typeof(DbContextOptions<CallCadenceDbContext>) ||
                        descriptor.ServiceType == typeof(DbContextOptions) ||
                        descriptor.ServiceType == typeof(CallCadenceDbContext)).ToList();

                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<CallCadenceDbContext>(options => options.UseInMemoryDatabase(databaseName));

                    services.RemoveAll(typeof(IGlobalConfiguration));
                    services.RemoveAll(typeof(IBackgroundJobClient));
                    services.RemoveAll(typeof(IRecurringJobManager));
                    services.RemoveAll(typeof(JobStorage));

                    services.AddHangfire(config => config.UseMemoryStorage());
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
    public async Task Add_ShouldNormalizeAndPersistTag()
    {
        var response = await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = " #Hello World " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<TagDto>();
        created.Should().NotBeNull();
        created!.Value.Should().Be("#hello_world");
    }

    [Test]
    public async Task Add_ShouldReturnBadRequest_WhenTagIsBlank()
    {
        var response = await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Add_ShouldNotCreateDuplicates_ForEquivalentNormalizedTags()
    {
        await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "#Hello World" });
        await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "hello   world" });

        var response = await _client.GetAsync("/api/Tags?query=hello");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().NotBeNull();
        tags.Should().ContainSingle(tag => tag.Value == "#hello_world");
    }

    [Test]
    public async Task Lookup_ShouldReturnMatchingTags_ForPartialQuery()
    {
        await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "#hello_world" });
        await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "#another_tag" });
        await _client.PostAsJsonAsync("/api/Tags", new CreateTagDto { Value = "#say_hello_world" });

        var response = await _client.GetAsync("/api/Tags?query=hello world");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().NotBeNull();
        tags!.Select(tag => tag.Value).Should().Equal("#hello_world", "#say_hello_world");
    }
}
