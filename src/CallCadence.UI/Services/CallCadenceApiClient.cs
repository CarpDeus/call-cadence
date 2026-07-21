using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Paging;
using CallCadence.Models.Auth;
using CallCadence.Application.Dashboard;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.SignalR.Client;

namespace CallCadence.UI.Services;

public sealed class CallCadenceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly UserSessionState _session;
    private readonly IJSRuntime _jsRuntime;

    public CallCadenceApiClient(HttpClient httpClient, UserSessionState session, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _session = session;
        _jsRuntime = jsRuntime;
    }

    public Uri BaseAddress => _httpClient.BaseAddress ?? new Uri("http://localhost:5108");

    public string GetApiUnavailableMessage()
    {
        return $"Unable to reach the API at {BaseAddress}. Start the API and refresh the page.";
    }

    public string GetErrorMessage(Exception exception, string fallbackMessage)
    {
        return exception switch
        {
            HttpRequestException => GetApiUnavailableMessage(),
            InvalidOperationException when exception.InnerException is SocketException => GetApiUnavailableMessage(),
            _ => fallbackMessage
        };
    }

    public HubConnection CreateHubConnection(string hubPath)
    {
        var hubUrl = new Uri(BaseAddress, hubPath);
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token);
            })
            .WithAutomaticReconnect()
            .Build();
    }

    public async Task<AuthStatusResponse> GetAuthStatusAsync()
    {
        return await _httpClient.GetFromJsonAsync<AuthStatusResponse>("api/auth/status")
            ?? new AuthStatusResponse();
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await _httpClient.PostAsync("api/auth/logout", null);
        }
        catch
        {
            // Best-effort server logout; local session is cleared regardless.
        }

        _session.SignOut();
        await _jsRuntime.InvokeVoidAsync("callCadenceAuth.clearToken");
    }

    public async Task<AuthResponse> RegisterAdminAsync(RegisterAdminRequest request)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Authentication failed."
            };

        await PersistSessionAsync(authResponse);
        return authResponse;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Authentication failed."
            };

        await PersistSessionAsync(authResponse);
        return authResponse;
    }

    public async Task<AuthResponse> ExchangeSsoCodeAsync(string code)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/sso/exchange", new SsoCodeExchangeRequest { Code = code });
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Authentication failed."
            };

        await PersistSessionAsync(authResponse);
        return authResponse;
    }

    private async Task PersistSessionAsync(AuthResponse authResponse)
    {
        if (!authResponse.Authenticated || string.IsNullOrWhiteSpace(authResponse.Email) || string.IsNullOrWhiteSpace(authResponse.Token))
        {
            return;
        }

        _session.SignIn(authResponse.Email, authResponse.IsAdmin, authResponse.Token, authResponse.ExpiresAtUtc);
        await _jsRuntime.InvokeVoidAsync(
            "callCadenceAuth.setToken",
            authResponse.Token,
            authResponse.Email,
            authResponse.IsAdmin,
            authResponse.ExpiresAtUtc?.ToString("o"));
    }

    public async Task<bool> RehydrateSessionAsync()
    {
        if (_session.IsAuthenticated)
        {
            return true;
        }

        var stored = await _jsRuntime.InvokeAsync<StoredSession?>("callCadenceAuth.getSession");
        if (stored is null || string.IsNullOrWhiteSpace(stored.Token) || string.IsNullOrWhiteSpace(stored.Email))
        {
            return false;
        }

        DateTime? expiresAtUtc = DateTime.TryParse(stored.ExpiresAtUtc, out var parsed) ? parsed.ToUniversalTime() : null;
        _session.SignIn(stored.Email, stored.IsAdmin, stored.Token, expiresAtUtc);
        return true;
    }


    public async Task<List<UserSummaryResponse>> GetUsersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserSummaryResponse>>("api/auth/users")
            ?? [];
    }

    public async Task<UserSummaryResponse> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/users", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserSummaryResponse>()
            ?? new UserSummaryResponse();
    }

    public async Task<UserSummaryResponse> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/auth/users/{Uri.EscapeDataString(userId)}", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserSummaryResponse>()
            ?? new UserSummaryResponse();
    }

    public async Task SetUserPasswordAsync(string userId, SetUserPasswordRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/auth/users/{Uri.EscapeDataString(userId)}/password", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeactivateUserAsync(string userId)
    {
        var response = await _httpClient.DeleteAsync($"api/auth/users/{Uri.EscapeDataString(userId)}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SsoConfigurationResponse>> GetSsoConfigurationsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<SsoConfigurationResponse>>("api/auth/sso")
            ?? [];
    }

    public async Task<SsoEnvironmentStatusResponse> GetSsoEnvironmentStatusAsync()
    {
        return await _httpClient.GetFromJsonAsync<SsoEnvironmentStatusResponse>("api/auth/sso/status")
            ?? new SsoEnvironmentStatusResponse();
    }

    public async Task<SsoConfigurationResponse> UpsertSsoConfigurationAsync(UpsertSsoConfigurationRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/auth/sso", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SsoConfigurationResponse>()
            ?? new SsoConfigurationResponse();
    }

    public async Task DeleteSsoConfigurationAsync(string schemeName)
    {
        var response = await _httpClient.DeleteAsync($"api/auth/sso/{Uri.EscapeDataString(schemeName)}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<DashboardStateDto> GetDashboardStateAsync()
    {
        return await _httpClient.GetFromJsonAsync<DashboardStateDto>("api/dashboard/state")
            ?? new DashboardStateDto();
    }

    public async Task ClearDashboardErrorsAsync(IEnumerable<Guid> errorIds)
    {
        await _httpClient.PostAsJsonAsync("api/dashboard/errors/clear", new ClearDashboardErrorsRequest
        {
            ErrorIds = errorIds.ToList()
        });
    }

    public async Task<List<ApiCallDto>> GetApiCallsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ApiCallDto>>("api/ApiCallManagement") ?? [];
    }

    public async Task<PagedResult<ApiCallListItemDto>> GetApiCallListAsync(ApiCallListRequest request, CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string>
        {
            $"pageNumber={request.PageNumber}",
            $"pageSize={request.PageSize}",
            $"sortBy={Uri.EscapeDataString(request.SortBy)}",
            $"sortDescending={request.SortDescending.ToString().ToLowerInvariant()}"
        };

        if (request.Enabled.HasValue)
        {
            queryParts.Add($"enabled={request.Enabled.Value.ToString().ToLowerInvariant()}");
        }

        var uri = $"api/ApiCallManagement/list?{string.Join("&", queryParts)}";
        return await _httpClient.GetFromJsonAsync<PagedResult<ApiCallListItemDto>>(uri, cancellationToken)
            ?? new PagedResult<ApiCallListItemDto>(new Paging(0, 0, request.PageNumber, request.PageSize), []);
    }

    public async Task<ApiCallDto?> GetApiCallAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<ApiCallDto>($"api/ApiCallManagement/{id}");
    }

    public async Task<List<ApiCallLogDto>> GetApiLogsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ApiCallLogDto>>("api/ApiCallScheduling/logs") ?? [];
    }

    public async Task<PagedResult<ApiCallLogDto>> GetApiCallLogsPagedAsync(Guid apiCallId, ApiCallLogListRequest request, CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string>
        {
            $"pageNumber={request.PageNumber}",
            $"pageSize={request.PageSize}",
            $"sortBy={Uri.EscapeDataString(request.SortBy)}",
            $"sortDescending={request.SortDescending.ToString().ToLowerInvariant()}"
        };

        var uri = $"api/ApiCallScheduling/logs/{apiCallId}/list?{string.Join("&", queryParts)}";
        return await _httpClient.GetFromJsonAsync<PagedResult<ApiCallLogDto>>(uri, cancellationToken)
            ?? new PagedResult<ApiCallLogDto>(new Paging(0, 0, request.PageNumber, request.PageSize), []);
    }

    public async Task<List<ScheduleInfoResponse>> GetSchedulesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ScheduleInfoResponse>>("api/ApiCallScheduling/schedules") ?? [];
    }

    public async Task<ScheduleResponse> ScheduleApiCallAsync(ScheduleRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ApiCallScheduling", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ScheduleResponse>()
            ?? throw new InvalidOperationException("Schedule API call returned no content.");
    }

    public async Task<List<ScheduleResponse>> ScheduleApiCallsAsync(IEnumerable<ScheduleRequest> requests)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ApiCallScheduling/bulk", requests);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>()
            ?? [];
    }

    public async Task RemoveScheduleAsync(string jobId)
    {
        var response = await _httpClient.DeleteAsync($"api/ApiCallScheduling/{Uri.EscapeDataString(jobId)}");
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveSchedulesAsync(IEnumerable<string> jobIds)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "api/ApiCallScheduling/bulk")
        {
            Content = JsonContent.Create(jobIds)
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiCallDto> CreateApiCallAsync(CreateApiCallDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ApiCallManagement", dto);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ApiCallDto>()
            ?? throw new InvalidOperationException("API call creation returned no content.");
    }

    public async Task<TestApiCallResponse> TestApiCallAsync(TestApiCallRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ApiCallManagement/test", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TestApiCallResponse>()
            ?? throw new InvalidOperationException("Test API call returned no content.");
    }

    public async Task<ApiCallDto> UpdateApiCallAsync(UpdateApiCallDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ApiCallManagement/{dto.Id}", dto);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ApiCallDto>()
            ?? throw new InvalidOperationException("API call update returned no content.");
    }

}
