using System.Net;
using System.Net.Http.Json;
using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
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
public sealed class ApiCallSchedulingControllerTests
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

                    // Remove all DbContext related services
                    var descriptorsToRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<CallCadenceDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(CallCadenceDbContext)).ToList();

                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory database for testing
                    services.AddDbContext<CallCadenceDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(databaseName);
                    });

                    // Remove Hangfire configuration and re-add with memory storage
                    services.RemoveAll(typeof(IGlobalConfiguration));
                    services.RemoveAll(typeof(IBackgroundJobClient));
                    services.RemoveAll(typeof(IRecurringJobManager));
                    services.RemoveAll(typeof(JobStorage));

                    // Add Hangfire with in-memory storage for testing
                    services.AddHangfire(config => config
                        .UseMemoryStorage());
                    services.AddSingleton<IRecurringJobManager>(sp =>
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

    private async Task<ApiCallDto> CreateTestApiCall(bool isActive = true)
    {
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            Description = "Test Description",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = isActive
        };

        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        return (await response.Content.ReadFromJsonAsync<ApiCallDto>())!;
    }

    [Test]
    public async Task ScheduleApiCall_ShouldScheduleSuccessfully()
    {
        // Arrange
        var apiCall = await CreateTestApiCall();
        var scheduleRequest = new ScheduleRequest
        {
            ApiCallId = apiCall.Id,
            CronExpression = "0 * * * *" // Every hour
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallScheduling", scheduleRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduleResponse = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        scheduleResponse.Should().NotBeNull();
        scheduleResponse!.ScheduleId.Should().NotBeEmpty();
        scheduleResponse!.JobId.Should().Be($"schedule-{scheduleResponse.ScheduleId}");
        scheduleResponse.ApiCallId.Should().Be(apiCall.Id);
        scheduleResponse.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task ScheduleApiCall_ShouldAllowMultipleSchedulesAndDisabledDefinitions()
    {
        // Arrange
        var apiCall = await CreateTestApiCall();

        var enabledScheduleRequest = new ScheduleRequest
        {
            ApiCallId = apiCall.Id,
            CronExpression = "0 * * * *",
            IsEnabled = true
        };

        var disabledScheduleRequest = new ScheduleRequest
        {
            ApiCallId = apiCall.Id,
            CronExpression = "*/5 * * * *",
            IsEnabled = false
        };

        // Act
        var enabledResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", enabledScheduleRequest);
        var disabledResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", disabledScheduleRequest);

        // Assert
        enabledResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        disabledResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var schedulesResponse = await _client.GetAsync("/api/ApiCallScheduling/schedules");
        var schedules = await schedulesResponse.Content.ReadFromJsonAsync<List<ScheduleInfoResponse>>();
        schedules.Should().NotBeNull();
        schedules.Should().HaveCount(2);
        schedules!.Should().ContainSingle(schedule => schedule.IsEnabled);
        schedules.Should().ContainSingle(schedule => !schedule.IsEnabled);
        schedules.Where(schedule => !schedule.IsEnabled).Single().NextExecution.Should().BeNull();
    }

    [Test]
    public async Task ScheduleApiCalls_ShouldScheduleMultipleSuccessfully()
    {
        // Arrange
        var apiCall1 = await CreateTestApiCall();
        var apiCall2 = await CreateTestApiCall();
        var scheduleRequests = new List<ScheduleRequest>
        {
            new ScheduleRequest { ApiCallId = apiCall1.Id, CronExpression = "0 * * * *" },
            new ScheduleRequest { ApiCallId = apiCall2.Id, CronExpression = "*/5 * * * *" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallScheduling/bulk", scheduleRequests);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduleResponses = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        scheduleResponses.Should().NotBeNull();
        scheduleResponses.Should().HaveCount(2);
    }

    [Test]
    public async Task RemoveSchedule_ShouldRemoveSuccessfully()
    {
        // Arrange
        var apiCall = await CreateTestApiCall();
        var scheduleRequest = new ScheduleRequest
        {
            ApiCallId = apiCall.Id,
            CronExpression = "0 * * * *"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", scheduleRequest);
        var scheduleResponse = await createResponse.Content.ReadFromJsonAsync<ScheduleResponse>();
        var jobId = scheduleResponse!.ScheduleId.ToString();

        // Act
        var response = await _client.DeleteAsync($"/api/ApiCallScheduling/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task RemoveSchedules_ShouldRemoveMultipleSuccessfully()
    {
        // Arrange
        var apiCall1 = await CreateTestApiCall();
        var apiCall2 = await CreateTestApiCall();
        var scheduleRequests = new List<ScheduleRequest>
        {
            new ScheduleRequest { ApiCallId = apiCall1.Id, CronExpression = "0 * * * *" },
            new ScheduleRequest { ApiCallId = apiCall2.Id, CronExpression = "*/5 * * * *" }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling/bulk", scheduleRequests);
        var scheduleResponses = await createResponse.Content.ReadFromJsonAsync<List<ScheduleResponse>>();

        var jobIds = scheduleResponses!.Select(schedule => schedule.ScheduleId.ToString()).ToList();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/ApiCallScheduling/bulk")
        {
            Content = JsonContent.Create(jobIds)
        };
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetSchedules_ShouldReturnScheduledJobs()
    {
        // Arrange
        var apiCall1 = await CreateTestApiCall();
        var apiCall2 = await CreateTestApiCall();
        var scheduleRequests = new List<ScheduleRequest>
        {
            new ScheduleRequest { ApiCallId = apiCall1.Id, CronExpression = "0 * * * *" },
            new ScheduleRequest { ApiCallId = apiCall2.Id, CronExpression = "*/5 * * * *" }
        };
        await _client.PostAsJsonAsync("/api/ApiCallScheduling/bulk", scheduleRequests);

        // Act
        var response = await _client.GetAsync("/api/ApiCallScheduling/schedules");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleInfoResponse>>();
        schedules.Should().NotBeNull();
        schedules.Should().HaveCount(2);
        schedules![0].Title.Should().Be("Test API");
        schedules[0].HttpMethod.Should().Be("GET");
        schedules[0].IsActive.Should().BeTrue();
        schedules[0].IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task RemoveInactive_ShouldRemoveJobsForInactiveApiCalls()
    {
        // Arrange
        var activeApiCall = await CreateTestApiCall(isActive: true);
        var inactiveApiCall = await CreateTestApiCall(isActive: false);
        
        var scheduleRequests = new List<ScheduleRequest>
        {
            new ScheduleRequest { ApiCallId = activeApiCall.Id, CronExpression = "0 * * * *" },
            new ScheduleRequest { ApiCallId = inactiveApiCall.Id, CronExpression = "*/5 * * * *" }
        };
        await _client.PostAsJsonAsync("/api/ApiCallScheduling/bulk", scheduleRequests);

        // Act
        var response = await _client.DeleteAsync("/api/ApiCallScheduling/inactive");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify that the inactive job was removed by checking schedules
        var schedulesResponse = await _client.GetAsync("/api/ApiCallScheduling/schedules");
        var schedules = await schedulesResponse.Content.ReadFromJsonAsync<List<ScheduleInfoResponse>>();
        schedules.Should().NotBeNull();
        schedules.Should().HaveCount(1);
        schedules![0].ApiCallId.Should().Be(activeApiCall.Id);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldRemoveScheduleAndSkipLog_WhenApiCallIsMissing()
    {
        // Arrange
        var apiCall = await CreateTestApiCall(isActive: true);
        var scheduleRequest = new ScheduleRequest
        {
            ApiCallId = apiCall.Id,
            CronExpression = "0 * * * *"
        };
        var createScheduleResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", scheduleRequest);
        var scheduleResponse = await createScheduleResponse.Content.ReadFromJsonAsync<ScheduleResponse>();
        scheduleResponse.Should().NotBeNull();

        using (var deleteScope = _factory.Services.CreateScope())
        {
            var dbContext = deleteScope.ServiceProvider.GetRequiredService<CallCadenceDbContext>();
            var existing = await dbContext.ApiCalls.FirstOrDefaultAsync(x => x.Id == apiCall.Id);
            existing.Should().NotBeNull();
            dbContext.ApiCalls.Remove(existing!);
            await dbContext.SaveChangesAsync();
        }

        // Act
        using (var executionScope = _factory.Services.CreateScope())
        {
            var callApiService = executionScope.ServiceProvider.GetRequiredService<CallApiService>();
            await callApiService.ExecuteApiCallAsync(apiCall.Id, Guid.NewGuid());
        }

        // Assert
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<CallCadenceDbContext>();
        var schedules = await verifyDbContext.ApiCallSchedules
            .Where(x => x.ApiCallId == apiCall.Id)
            .ToListAsync();
        schedules.Should().BeEmpty();

        var logs = await verifyDbContext.ApiCallLogs
            .Where(x => x.ApiCallId == apiCall.Id)
            .ToListAsync();
        logs.Should().BeEmpty();
    }
}
