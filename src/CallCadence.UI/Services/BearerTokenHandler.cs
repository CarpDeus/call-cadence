using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace CallCadence.UI.Services;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly UserSessionState _session;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(UserSessionState session, ILogger<BearerTokenHandler> logger)
    {
        _session = session;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_session.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
            _logger.LogDebug(
                "Auth: attaching bearer token to {Method} {RequestUri} for user {Email}.",
                request.Method,
                request.RequestUri,
                _session.Email);
        }
        else
        {
            _logger.LogDebug(
                "Auth: API call to {Method} {RequestUri} sent without authentication: no bearer token is present in the current session.",
                request.Method,
                request.RequestUri);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Auth: {Method} {RequestUri} returned {StatusCode}. Token present: {HasToken}. " +
                "The API rejected the credentials (expired/invalid token or insufficient role).",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode,
                !string.IsNullOrWhiteSpace(_session.Token));
        }

        return response;
    }
}
