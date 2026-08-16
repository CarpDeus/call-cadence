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
        }
        else
        {
            _logger.LogDebug(
                "API call to {RequestUri} sent without authentication: no bearer token is present in the current session.",
                request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
