using System.Net.Http.Headers;

namespace CallCadence.UI.Services;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly UserSessionState _session;

    public BearerTokenHandler(UserSessionState session)
    {
        _session = session;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_session.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
