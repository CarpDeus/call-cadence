using System.Net;
using System.Net.Http.Json;
using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Paging;
using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using CallCadence.IntegrationTests.TestAuthentication;

namespace CallCadence.IntegrationTests.Controllers;

[TestFixture]
public sealed class ApiCallManagementControllerTests
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

    [Test]
    public async Task GetAll_ShouldReturnEmptyList_Initially()
    {
        // Act
        var response = await _client.GetAsync("/api/ApiCallManagement");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiCalls = await response.Content.ReadFromJsonAsync<List<ApiCallDto>>();
        apiCalls.Should().NotBeNull();
        apiCalls.Should().BeEmpty();
    }

    [Test]
    public async Task GetList_ShouldIncludeAllApisByDefaultAndIncludeScheduleAndLogMetadata()
    {
        // Arrange
        var enabledApi = await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Enabled API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/enabled",
            IsActive = true
        });

        var disabledApi = await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Disabled API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/disabled",
            IsActive = false
        });

        await _client.PostAsJsonAsync("/api/ApiCallScheduling", new ScheduleRequest
        {
            ApiCallId = enabledApi.Id,
            CronExpression = "0 * * * *"
        });

        await AddLogAsync(new ApiCallLog
        {
            Id = Guid.NewGuid(),
            ApiCallId = enabledApi.Id,
            ExecutedAt = DateTime.UtcNow.AddMinutes(-15),
            DurationMs = 25,
            Success = true
        });

        await AddLogAsync(new ApiCallLog
        {
            Id = Guid.NewGuid(),
            ApiCallId = enabledApi.Id,
            ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
            DurationMs = 30,
            Success = false,
            ErrorMessage = "boom"
        });

        // Act
        var response = await _client.GetAsync("/api/ApiCallManagement/list");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ApiCallListItemDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Select(x => x.Id).Should().Contain(enabledApi.Id);
        result.Items.Select(x => x.Id).Should().Contain(disabledApi.Id);

        var item = result.Items.Single(x => x.Id == enabledApi.Id);
        item.IsActive.Should().BeTrue();
        item.HasSchedule.Should().BeTrue();
        item.NextScheduledCall.Should().NotBeNull();
        item.LastSuccessAt.Should().NotBeNull();
        item.LastErrorAt.Should().NotBeNull();
        result.Paging.TotalItems.Should().Be(2);
    }

    [Test]
    public async Task GetList_ShouldTreatDisabledSchedulesAsDefinedButNotRunnable()
    {
        // Arrange
        var disabledApi = await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Disabled API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/disabled",
            IsActive = false
        });

        await _client.PostAsJsonAsync("/api/ApiCallScheduling", new ScheduleRequest
        {
            ApiCallId = disabledApi.Id,
            CronExpression = "0 * * * *",
            IsEnabled = false
        });

        // Act
        var response = await _client.GetAsync("/api/ApiCallManagement/list?enabled=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ApiCallListItemDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle();

        var item = result.Items.Single();
        item.Id.Should().Be(disabledApi.Id);
        item.HasSchedule.Should().BeTrue();
        item.NextScheduledCall.Should().BeNull();
    }

    [Test]
    public async Task GetList_ShouldApplySortingPagingAndFiltering()
    {
        // Arrange
        await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Alpha",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/a",
            IsActive = false
        });

        await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Charlie",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/c",
            IsActive = false
        });

        await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Bravo",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/b",
            IsActive = false
        });

        // Act
        var response = await _client.GetAsync("/api/ApiCallManagement/list?enabled=false&sortBy=title&sortDescending=true&pageNumber=2&pageSize=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ApiCallListItemDto>>();
        result.Should().NotBeNull();
        result!.Paging.TotalItems.Should().Be(3);
        result.Paging.PageNumber.Should().Be(2);
        result.Items.Select(item => item.Title).Should().Equal("Bravo");
    }

    [Test]
    public async Task Create_ShouldCreateApiCall()
    {
        // Arrange
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            Description = "Test Description",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ApiCallDto>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Title.Should().Be(createDto.Title);
        created.Description.Should().Be(createDto.Description);
    }

    [Test]
    public async Task GetById_ShouldReturnApiCall_WhenExists()
    {
        // Arrange - Create an API call first
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiCallDto>();

        // Act
        var response = await _client.GetAsync($"/api/ApiCallManagement/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiCall = await response.Content.ReadFromJsonAsync<ApiCallDto>();
        apiCall.Should().NotBeNull();
        apiCall!.Id.Should().Be(created.Id);
    }

    [Test]
    public async Task GetById_ShouldReturnNotFound_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/ApiCallManagement/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Activate_ShouldActivateApiCall_WhenInactive()
    {
        // Arrange - Create an inactive API call
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = false
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiCallDto>();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{created!.Id}/Activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's now active
        var getResponse = await _client.GetAsync($"/api/ApiCallManagement/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<ApiCallDto>();
        updated!.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task Activate_ShouldReturnOk_WhenAlreadyActive()
    {
        // Arrange - Create an active API call
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = true
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiCallDto>();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{created!.Id}/Activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Activate_ShouldReturnNotFound_WhenApiCallNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{nonExistentId}/Activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Deactivate_ShouldDeactivateApiCall_WhenActive()
    {
        // Arrange - Create an active API call
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = true
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiCallDto>();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{created!.Id}/Deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's now inactive
        var getResponse = await _client.GetAsync($"/api/ApiCallManagement/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<ApiCallDto>();
        updated!.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task Deactivate_ShouldRemoveSchedulesForApiCall()
    {
        // Arrange
        var created = await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Scheduled API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/scheduled",
            IsActive = true
        });

        var scheduleResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", new ScheduleRequest
        {
            ApiCallId = created.Id,
            CronExpression = "0 * * * *",
            IsEnabled = true
        });
        scheduleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var deactivateResponse = await _client.PostAsync($"/api/ApiCallManagement/{created.Id}/Deactivate", null);

        // Assert
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedulesResponse = await _client.GetAsync("/api/ApiCallScheduling/schedules");
        var schedules = await schedulesResponse.Content.ReadFromJsonAsync<List<ScheduleInfoResponse>>();
        schedules.Should().NotBeNull();
        schedules!.Should().NotContain(schedule => schedule.ApiCallId == created.Id);
    }

    [Test]
    public async Task Deactivate_ShouldReturnOk_WhenAlreadyInactive()
    {
        // Arrange - Create an inactive API call
        var createDto = new CreateApiCallDto
        {
            Title = "Test API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/test",
            IsActive = false
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiCallDto>();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{created!.Id}/Deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Deactivate_ShouldReturnNotFound_WhenApiCallNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/ApiCallManagement/{nonExistentId}/Deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateMany_ShouldCreateMultipleApiCalls()
    {
        // Arrange
        var createDtos = new List<CreateApiCallDto>
        {
            new CreateApiCallDto
            {
                Title = "Test API 1",
                Description = "Test Description 1",
                HttpMethod = "GET",
                EndpointUrl = "https://api.example.com/test1",
                IsActive = true
            },
            new CreateApiCallDto
            {
                Title = "Test API 2",
                Description = "Test Description 2",
                HttpMethod = "POST",
                EndpointUrl = "https://api.example.com/test2",
                IsActive = false
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk", createDtos);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<List<ApiCallDto>>();
        created.Should().NotBeNull();
        created.Should().HaveCount(2);
        created![0].Title.Should().Be("Test API 1");
        created[1].Title.Should().Be("Test API 2");
    }

    [Test]
    public async Task UpdateMany_ShouldUpdateMultipleApiCalls()
    {
        // Arrange - Create API calls first
        var createDtos = new List<CreateApiCallDto>
        {
            new CreateApiCallDto { Title = "Test API 1", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test1" },
            new CreateApiCallDto { Title = "Test API 2", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test2" }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk", createDtos);
        var created = await createResponse.Content.ReadFromJsonAsync<List<ApiCallDto>>();

        // Create update DTOs
        var updateDtos = created!.Select(c => new UpdateApiCallDto
        {
            Id = c.Id,
            Title = c.Title + " Updated",
            Description = c.Description,
            HttpMethod = "POST",
            EndpointUrl = c.EndpointUrl,
            IsActive = c.IsActive
        }).ToList();

        // Act
        var response = await _client.PutAsJsonAsync("/api/ApiCallManagement/bulk", updateDtos);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<List<ApiCallDto>>();
        updated.Should().NotBeNull();
        updated.Should().HaveCount(2);
        updated![0].Title.Should().Contain("Updated");
        updated[0].HttpMethod.Should().Be("POST");
    }

    [Test]
    public async Task ActivateMany_ShouldActivateMultipleApiCalls()
    {
        // Arrange - Create inactive API calls
        var createDtos = new List<CreateApiCallDto>
        {
            new CreateApiCallDto { Title = "Test API 1", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test1", IsActive = false },
            new CreateApiCallDto { Title = "Test API 2", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test2", IsActive = false }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk", createDtos);
        var created = await createResponse.Content.ReadFromJsonAsync<List<ApiCallDto>>();
        var ids = created!.Select(c => c.Id).ToList();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk/Activate", ids);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify they're now active
        foreach (var id in ids)
        {
            var getResponse = await _client.GetAsync($"/api/ApiCallManagement/{id}");
            var apiCall = await getResponse.Content.ReadFromJsonAsync<ApiCallDto>();
            apiCall!.IsActive.Should().BeTrue();
        }
    }

    [Test]
    public async Task DeactivateMany_ShouldDeactivateMultipleApiCalls()
    {
        // Arrange - Create active API calls
        var createDtos = new List<CreateApiCallDto>
        {
            new CreateApiCallDto { Title = "Test API 1", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test1", IsActive = true },
            new CreateApiCallDto { Title = "Test API 2", HttpMethod = "GET", EndpointUrl = "https://api.example.com/test2", IsActive = true }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk", createDtos);
        var created = await createResponse.Content.ReadFromJsonAsync<List<ApiCallDto>>();
        var ids = created!.Select(c => c.Id).ToList();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement/bulk/Deactivate", ids);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify they're now inactive
        foreach (var id in ids)
        {
            var getResponse = await _client.GetAsync($"/api/ApiCallManagement/{id}");
            var apiCall = await getResponse.Content.ReadFromJsonAsync<ApiCallDto>();
            apiCall!.IsActive.Should().BeFalse();
        }
    }

    [Test]
    public async Task Update_ShouldRemoveSchedules_WhenApiCallBecomesInactive()
    {
        // Arrange
        var created = await CreateApiCallAsync(new CreateApiCallDto
        {
            Title = "Update Scheduled API",
            HttpMethod = "GET",
            EndpointUrl = "https://api.example.com/update-scheduled",
            IsActive = true
        });

        var scheduleResponse = await _client.PostAsJsonAsync("/api/ApiCallScheduling", new ScheduleRequest
        {
            ApiCallId = created.Id,
            CronExpression = "0 * * * *",
            IsEnabled = true
        });
        scheduleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateDto = new UpdateApiCallDto
        {
            Id = created.Id,
            Title = created.Title,
            Description = created.Description,
            HttpMethod = created.HttpMethod,
            EndpointUrl = created.EndpointUrl,
            Payload = created.Payload,
            Headers = created.Headers,
            Parameters = created.Parameters,
            IsActive = false
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/ApiCallManagement/{created.Id}", updateDto);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedulesResponse = await _client.GetAsync("/api/ApiCallScheduling/schedules");
        var schedules = await schedulesResponse.Content.ReadFromJsonAsync<List<ScheduleInfoResponse>>();
        schedules.Should().NotBeNull();
        schedules!.Should().NotContain(schedule => schedule.ApiCallId == created.Id);
    }

    private async Task<ApiCallDto> CreateApiCallAsync(CreateApiCallDto dto)
    {
        var response = await _client.PostAsJsonAsync("/api/ApiCallManagement", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApiCallDto>())!;
    }

    private async Task AddLogAsync(ApiCallLog log)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CallCadenceDbContext>();
        dbContext.ApiCallLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }
}
