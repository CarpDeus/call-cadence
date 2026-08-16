using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CallCadence.Models.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CallCadence.UI.Services;

/// <summary>
/// Provides authentication state for the Blazor UI by persisting the API-issued JWT
/// in encrypted <see cref="ProtectedLocalStorage"/> and projecting it into a
/// <see cref="ClaimsPrincipal"/> used by <c>&lt;AuthorizeRouteView&gt;</c>, <c>[Authorize]</c>
/// and <c>&lt;AuthorizeView&gt;</c>.
/// </summary>
public sealed class TokenAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "callcadence.session";

    /// <summary>
    /// Role name that must match the API's <c>ApplicationRoles.Admin</c> value so that
    /// <c>[Authorize(Roles = "Admin")]</c> gating works against the projected principal.
    /// </summary>
    public const string AdminRole = "Admin";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly ProtectedLocalStorage _protectedStorage;
    private readonly UserSessionState _session_state;
    private readonly ILogger<TokenAuthenticationStateProvider> _logger;

    private StoredSession? _session;
    private bool _loadedFromStorage;

    public TokenAuthenticationStateProvider(
        ProtectedLocalStorage protectedStorage,
        UserSessionState sessionState,
        ILogger<TokenAuthenticationStateProvider> logger)
    {
        _protectedStorage = protectedStorage;
        _session_state = sessionState;
        _logger = logger;
    }

    private void SyncSessionState()
    {
        if (IsSessionValid(_session))
        {
            _session_state.SignIn(_session!.Email!, _session.IsAdmin, _session.Token, ExpiresAtUtc);
        }
        else
        {
            _session_state.SignOut();
        }
    }

    /// <summary>
    /// The current bearer token, or <c>null</c> when no valid session is loaded.
    /// Exposed synchronously so <see cref="BearerTokenHandler"/> and SignalR can read it.
    /// </summary>
    public string? Token => IsSessionValid(_session) ? _session!.Token : null;

    public string? Email => IsSessionValid(_session) ? _session!.Email : null;

    public bool IsAdmin => IsSessionValid(_session) && _session!.IsAdmin;

    public bool IsAuthenticated => IsSessionValid(_session);

    public DateTime? ExpiresAtUtc =>
        _session is not null && DateTime.TryParse(_session.ExpiresAtUtc, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureLoadedAsync();
        SyncSessionState();

        if (!IsSessionValid(_session))
        {
            return Anonymous;
        }

        _logger.LogDebug(
            "Auth: authentication state resolved for {Email} (admin: {IsAdmin}).",
            _session!.Email,
            _session.IsAdmin);

        return new AuthenticationState(BuildPrincipal(_session));
    }

    /// <summary>
    /// Persists a successful authentication result to encrypted storage and notifies
    /// the authentication system that the state changed.
    /// </summary>
    public async Task SignInAsync(AuthResponse response)
    {
        if (!response.Authenticated
            || string.IsNullOrWhiteSpace(response.Email)
            || string.IsNullOrWhiteSpace(response.Token))
        {
            _logger.LogWarning("Auth: SignInAsync called with an unauthenticated or incomplete response; ignoring.");
            return;
        }

        _session = new StoredSession
        {
            Token = response.Token,
            Email = response.Email,
            IsAdmin = response.IsAdmin,
            ExpiresAtUtc = response.ExpiresAtUtc?.ToString("o")
        };
        _loadedFromStorage = true;

        try
        {
            await _protectedStorage.SetAsync(StorageKey, _session);
            _logger.LogInformation("Auth: sign-in persisted for {Email} (admin: {IsAdmin}).", _session.Email, _session.IsAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth: failed to persist session to protected storage; continuing with in-memory session.");
        }

        SyncSessionState();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(_session))));
    }

    /// <summary>
    /// Clears the persisted session and notifies the authentication system.
    /// </summary>
    public async Task SignOutAsync()
    {
        _session = null;
        _loadedFromStorage = true;

        try
        {
            await _protectedStorage.DeleteAsync(StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth: failed to delete session from protected storage.");
        }

        SyncSessionState();
        _logger.LogInformation("Auth: user signed out; session cleared.");
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loadedFromStorage)
        {
            return;
        }

        try
        {
            var result = await _protectedStorage.GetAsync<StoredSession>(StorageKey);
            _session = result.Success ? result.Value : null;
            _loadedFromStorage = true;

            if (IsSessionValid(_session))
            {
                _logger.LogDebug("Auth: session rehydrated from protected storage for {Email}.", _session!.Email);
            }
            else if (_session is not null)
            {
                _logger.LogInformation("Auth: stored session for {Email} is expired or invalid; treating as anonymous.", _session.Email);
                _session = null;
            }
        }
        catch (InvalidOperationException)
        {
            // ProtectedBrowserStorage relies on JS interop, which is unavailable during
            // static prerendering. Remain anonymous until the interactive circuit starts;
            // do not mark as loaded so the state is re-read once interop is available.
            _logger.LogDebug("Auth: protected storage not available yet (prerender); deferring session load.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth: failed to read session from protected storage; treating as anonymous.");
            _loadedFromStorage = true;
            _session = null;
        }
    }

    private static bool IsSessionValid(StoredSession? session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.Token) || string.IsNullOrWhiteSpace(session.Email))
        {
            return false;
        }

        if (DateTime.TryParse(session.ExpiresAtUtc, out var expires)
            && expires.ToUniversalTime() <= DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    private static ClaimsPrincipal BuildPrincipal(StoredSession session)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, session.Email!),
            new(ClaimTypes.Email, session.Email!)
        };

        if (session.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        }

        // Surface any additional claims carried in the JWT (best-effort; the API is the
        // authority that validated the signature, so we only read the payload here).
        foreach (var jwtClaim in ReadJwtRoleClaims(session.Token!))
        {
            if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == jwtClaim))
            {
                claims.Add(new Claim(ClaimTypes.Role, jwtClaim));
            }
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> ReadJwtRoleClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            yield break;
        }

        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(token);
        }
        catch
        {
            yield break;
        }

        foreach (var claim in jwt.Claims)
        {
            if (claim.Type is ClaimTypes.Role or "role" or "roles")
            {
                yield return claim.Value;
            }
        }
    }
}
