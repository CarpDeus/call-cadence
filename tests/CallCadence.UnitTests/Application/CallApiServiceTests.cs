using System.Globalization;
using System.Net;
using System.Net.Http;
using BugLogger.Interfaces;
using CallCadence.API.Dashboard;
using CallCadence.API.Hubs;
using CallCadence.Domain.ApiCall;
using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using Hangfire;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class CallApiServiceTests
{
    [Test]
    public async Task ExecuteApiCallAsync_ShouldApplyMacroSubstitutionToParametersHeadersAndPayload()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var apiCallId = Guid.NewGuid();
        var capturedRequest = default(HttpRequestMessage);
        var capturedLog = default(ApiCallLog);
        var handler = new TestHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        });

        var httpClient = new HttpClient(handler);
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Test",
            Description = "Test",
            HttpMethod = "POST",
            EndpointUrl = "https://example.com/api",
            IsActive = true,
            Parameters =
            [
                new NamedValue { Name = "StartDate", Value = "@@yyyy@@-@@MMM@@-@@dd@@" },
                new NamedValue { Name = "KeepUnknown", Value = "@@NotSupported@@" }
            ],
            Headers =
            [
                new NamedValue { Name = "X-Month", Value = "@@MM@@" },
                new NamedValue { Name = "Authorization", Value = "Bearer @@yyyy@@" }
            ],
            Payload = "{\"year\":\"@@yyyy@@\",\"unknown\":\"@@NotSupported@@\"}"
        };

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        mockLogRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallLog>()))
            .ReturnsAsync((ApiCallLog log) =>
            {
                capturedLog = log;
                return log;
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockSentryService = new Mock<ISentryService>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            CreateDbContext(),
            new Mock<IRecurringJobManager>().Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, Guid.NewGuid());

        // Assert
        capturedRequest.Should().NotBeNull();

        var decodedQuery = Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query);
        decodedQuery.Should().Contain("StartDate=");
        decodedQuery.Should().Contain(now.ToString("yyyy", CultureInfo.InvariantCulture));
        decodedQuery.Should().Contain("KeepUnknown=@@NotSupported@@");

        capturedRequest.Headers.TryGetValues("X-Month", out var headerValues).Should().BeTrue();
        headerValues.Should().NotBeNull();
        headerValues!.Single().Should().Be(now.ToString("MM", CultureInfo.InvariantCulture));

        var payload = await capturedRequest.Content!.ReadAsStringAsync();
        payload.Should().Contain($"\"year\":\"{now.ToString("yyyy", CultureInfo.InvariantCulture)}\"");
        payload.Should().Contain("\"unknown\":\"@@NotSupported@@\"");

        capturedLog.Should().NotBeNull();
        capturedLog!.RequestUri.Should().NotBeNull();
        capturedLog.RequestUri.Should().Contain("https://example.com/api?");
        capturedLog.RequestUri.Should().Contain("StartDate=");
        capturedLog.RequestParameters.Should().Contain(x => x.Name == "StartDate");
        capturedLog.RequestParameters.Should().Contain(x => x.Name == "KeepUnknown" && x.Value == "@@NotSupported@@");
        capturedLog.RequestHeaders.Should().Contain(x => x.Name == "X-Month" && x.Value == now.ToString("MM", CultureInfo.InvariantCulture));
        capturedLog.RequestHeaders.Should().Contain(x => x.Name == "Authorization" && x.Value == "***");
        capturedLog.RequestBody.Should().Contain($"\"year\":\"{now.ToString("yyyy", CultureInfo.InvariantCulture)}\"");

        mockLogRepository.Verify(r => r.CreateAsync(It.Is<ApiCallLog>(l => l.Success)), Times.Once);
    }


    private static Mock<IHubContext<GenericHub>> CreateHubContextMock()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(x => x.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<GenericHub>>();
        hubContext.Setup(x => x.Clients).Returns(hubClients.Object);
        return hubContext;
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldApplyEvalMacroToParameters()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var apiCallId = Guid.NewGuid();
        var capturedRequest = default(HttpRequestMessage);
        var handler = new TestHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        });

        var httpClient = new HttpClient(handler);
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Test",
            Description = "Test",
            HttpMethod = "GET",
            EndpointUrl = "https://example.com/api",
            IsActive = true,
            Parameters =
            [
                new NamedValue { Name = "startDate", Value = "@@eval:today - 5 days:yyyy-MM-dd@@" },
                new NamedValue { Name = "endDate", Value = "@@eval:today:yyyy-MM-dd@@" }
            ]
        };

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        mockLogRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallLog>()))
            .ReturnsAsync((ApiCallLog log) => log);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockSentryService = new Mock<ISentryService>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            CreateDbContext(),
            new Mock<IRecurringJobManager>().Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, Guid.NewGuid());

        // Assert
        capturedRequest.Should().NotBeNull();

        var decodedQuery = Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query);
        var expectedStartDate = now.Date.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedEndDate = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        decodedQuery.Should().Contain($"startDate={expectedStartDate}");
        decodedQuery.Should().Contain($"endDate={expectedEndDate}");

        mockLogRepository.Verify(r => r.CreateAsync(It.Is<ApiCallLog>(l => l.Success)), Times.Once);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldApplyEvalMacroToHeaders()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var apiCallId = Guid.NewGuid();
        var capturedRequest = default(HttpRequestMessage);
        var handler = new TestHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        });

        var httpClient = new HttpClient(handler);
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Test",
            Description = "Test",
            HttpMethod = "GET",
            EndpointUrl = "https://example.com/api",
            IsActive = true,
            Headers =
            [
                new NamedValue { Name = "X-Request-Date", Value = "@@eval:today:yyyy-MM-dd@@" },
                new NamedValue { Name = "X-Days-Ago", Value = "@@eval:5 + 2@@" }
            ]
        };

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        mockLogRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallLog>()))
            .ReturnsAsync((ApiCallLog log) => log);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockSentryService = new Mock<ISentryService>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            CreateDbContext(),
            new Mock<IRecurringJobManager>().Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, Guid.NewGuid());

        // Assert
        capturedRequest.Should().NotBeNull();

        capturedRequest!.Headers.TryGetValues("X-Request-Date", out var dateHeaderValues).Should().BeTrue();
        var expectedDate = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        dateHeaderValues!.Single().Should().Be(expectedDate);

        capturedRequest.Headers.TryGetValues("X-Days-Ago", out var numericHeaderValues).Should().BeTrue();
        numericHeaderValues!.Single().Should().Be("7");

        mockLogRepository.Verify(r => r.CreateAsync(It.Is<ApiCallLog>(l => l.Success)), Times.Once);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldApplyEvalMacroToPayload()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var apiCallId = Guid.NewGuid();
        var capturedRequest = default(HttpRequestMessage);
        var handler = new TestHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        });

        var httpClient = new HttpClient(handler);
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Test",
            Description = "Test",
            HttpMethod = "POST",
            EndpointUrl = "https://example.com/api",
            IsActive = true,
            Payload = "{\"startDate\":\"@@eval:today - 7 days:yyyy-MM-dd@@\",\"endDate\":\"@@eval:today:yyyy-MM-dd@@\",\"count\":\"@@eval:10 + 5@@\"}"
        };

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        mockLogRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallLog>()))
            .ReturnsAsync((ApiCallLog log) => log);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockSentryService = new Mock<ISentryService>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            CreateDbContext(),
            new Mock<IRecurringJobManager>().Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, Guid.NewGuid());

        // Assert
        capturedRequest.Should().NotBeNull();

        var payload = await capturedRequest!.Content!.ReadAsStringAsync();
        var expectedStartDate = now.Date.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedEndDate = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        payload.Should().Contain($"\"startDate\":\"{expectedStartDate}\"");
        payload.Should().Contain($"\"endDate\":\"{expectedEndDate}\"");
        payload.Should().Contain("\"count\":\"15\"");

        mockLogRepository.Verify(r => r.CreateAsync(It.Is<ApiCallLog>(l => l.Success)), Times.Once);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldHandleMixedMacrosInPayload()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var apiCallId = Guid.NewGuid();
        var capturedRequest = default(HttpRequestMessage);
        var handler = new TestHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        });

        var httpClient = new HttpClient(handler);
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Test",
            Description = "Test",
            HttpMethod = "POST",
            EndpointUrl = "https://example.com/api",
            IsActive = true,
            Payload = "{\"date\":\"@@eval:today:yyyy-MM-dd@@\",\"year\":\"@@yyyy@@\",\"month\":\"@@MM@@\"}"
        };

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        mockLogRepository.Setup(r => r.CreateAsync(It.IsAny<ApiCallLog>()))
            .ReturnsAsync((ApiCallLog log) => log);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockSentryService = new Mock<ISentryService>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            CreateDbContext(),
            new Mock<IRecurringJobManager>().Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, Guid.NewGuid());

        // Assert
        capturedRequest.Should().NotBeNull();

        var payload = await capturedRequest!.Content!.ReadAsStringAsync();
        var expectedDate = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedYear = now.ToString("yyyy", CultureInfo.InvariantCulture);
        var expectedMonth = now.ToString("MM", CultureInfo.InvariantCulture);

        payload.Should().Contain($"\"date\":\"{expectedDate}\"");
        payload.Should().Contain($"\"year\":\"{expectedYear}\"");
        payload.Should().Contain($"\"month\":\"{expectedMonth}\"");

        mockLogRepository.Verify(r => r.CreateAsync(It.Is<ApiCallLog>(l => l.Success)), Times.Once);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldSkipLoggingAndUnschedule_WhenApiCallIsInactive()
    {
        // Arrange
        var apiCallId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var apiCall = new ApiCall
        {
            Id = apiCallId,
            Title = "Inactive API",
            Description = "Test",
            HttpMethod = "GET",
            EndpointUrl = "https://example.com/api",
            IsActive = false
        };

        var dbContext = CreateDbContext();
        dbContext.ApiCallSchedules.Add(new ApiCallSchedule
        {
            Id = scheduleId,
            ApiCallId = apiCallId,
            CronExpression = "0 * * * *",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync(apiCall);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockSentryService = new Mock<ISentryService>();
        var mockRecurringJobManager = new Mock<IRecurringJobManager>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            dbContext,
            mockRecurringJobManager.Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, scheduleId);

        // Assert
        mockLogRepository.Verify(r => r.CreateAsync(It.IsAny<ApiCallLog>()), Times.Never);
        mockRecurringJobManager.Verify(r => r.RemoveIfExists($"schedule-{scheduleId}"), Times.Once);
        (await dbContext.ApiCallSchedules.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task ExecuteApiCallAsync_ShouldSkipLoggingAndUnschedule_WhenApiCallIsMissing()
    {
        // Arrange
        var apiCallId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        var dbContext = CreateDbContext();
        dbContext.ApiCallSchedules.Add(new ApiCallSchedule
        {
            Id = scheduleId,
            ApiCallId = apiCallId,
            CronExpression = "0 * * * *",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var mockApiCallRepository = new Mock<IApiCallRepository>();
        mockApiCallRepository.Setup(r => r.GetByIdAsync(apiCallId))
            .ReturnsAsync((ApiCall?)null);

        var mockLogRepository = new Mock<IApiCallLogRepository>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockSentryService = new Mock<ISentryService>();
        var mockRecurringJobManager = new Mock<IRecurringJobManager>();

        var service = new CallApiService(
            mockApiCallRepository.Object,
            mockLogRepository.Object,
            mockHttpClientFactory.Object,
            mockSentryService.Object,
            new ApiCallActivityTracker(),
            CreateHubContextMock().Object,
            dbContext,
            mockRecurringJobManager.Object);

        // Act
        await service.ExecuteApiCallAsync(apiCallId, scheduleId);

        // Assert
        mockLogRepository.Verify(r => r.CreateAsync(It.IsAny<ApiCallLog>()), Times.Never);
        mockRecurringJobManager.Verify(r => r.RemoveIfExists($"schedule-{scheduleId}"), Times.Once);
        (await dbContext.ApiCallSchedules.CountAsync()).Should().Be(0);
    }

    private static CallCadenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CallCadenceDbContext>()
            .UseInMemoryDatabase($"CallApiServiceTests_{Guid.NewGuid()}")
            .Options;
        return new CallCadenceDbContext(options);
    }
}
