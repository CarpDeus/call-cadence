using System.Security.Claims;
using CallCadence.API.Auth;
using CallCadence.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace CallCadence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly ISsoConfigurationService _ssoConfigurationService;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly UserManager<AdminUser> _userManager;

    public AuthController(
        IAdminAuthService adminAuthService,
        ISsoConfigurationService ssoConfigurationService,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        UserManager<AdminUser> userManager)
    {
        _adminAuthService = adminAuthService;
        _ssoConfigurationService = ssoConfigurationService;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _jwtTokenService = jwtTokenService;
        _userManager = userManager;
    }

    [HttpGet("status")]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus()
    {
        var status = await _adminAuthService.GetStatusAsync(User);
        var allProviders = await _ssoConfigurationService.GetAllAsync();

        status.SsoProviders = allProviders
            .Where(provider => provider.IsEnabled)
            .Select(provider => new SsoProviderInfo
            {
                Name = provider.Name,
                SchemeName = provider.SchemeName,
                SignInUrl = Url.ActionLink(nameof(SsoChallenge), values: new { provider = provider.SchemeName }) ?? string.Empty
            })
            .ToList();

        return Ok(status);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _adminAuthService.LogoutAsync();
        return NoContent();
    }

    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterAdminRequest request)
    {
        var result = await _adminAuthService.RegisterAdminAsync(request);
        if (!result.Authenticated)
        {
            return Conflict(result);
        }

        return Ok(result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _adminAuthService.LoginAsync(request);
        if (!result.Authenticated)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetUsers()
    {
        return Ok(await _adminAuthService.GetUsersAsync());
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("auth")]
    [HttpPost("users")]
    public async Task<ActionResult<UserSummaryResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var createdUser = await _adminAuthService.CreateUserAsync(request);
            return CreatedAtAction(nameof(GetUsers), value: createdUser);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("users/{userId}")]
    public async Task<ActionResult<UserSummaryResponse>> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updatedUser = await _adminAuthService.UpdateUserAsync(userId, request, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Ok(updatedUser);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "User not found."
                ? NotFound(ex.Message)
                : BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("auth")]
    [HttpPut("users/{userId}/password")]
    public async Task<IActionResult> SetPassword(string userId, [FromBody] SetUserPasswordRequest request)
    {
        try
        {
            await _adminAuthService.SetPasswordAsync(userId, request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "User not found."
                ? NotFound(ex.Message)
                : BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeactivateUser(string userId)
    {
        try
        {
            await _adminAuthService.DeactivateUserAsync(userId, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "User not found."
                ? NotFound(ex.Message)
                : BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("sso-challenge")]
    public async Task<IActionResult> SsoChallenge([FromQuery] string? provider = null, [FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest("A provider scheme name is required.");
        }

        var config = await _ssoConfigurationService.GetBySchemeNameAsync(provider);
        if (config is null || !config.IsEnabled)
        {
            return BadRequest($"SSO provider '{provider}' is not configured or not enabled.");
        }

        var callbackUrl = Url.Action(nameof(SsoCallback), values: new { provider, returnUrl });
        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl
        };
        return Challenge(properties, provider);
    }

    [Authorize]
    [HttpGet("sso-callback")]
    public async Task<IActionResult> SsoCallback([FromQuery] string? provider = null, [FromQuery] string? returnUrl = null)
    {
        var allowedUrls = _configuration.GetAllowedUiReturnUrls();
        if (string.IsNullOrWhiteSpace(returnUrl) || !allowedUrls.Any(allowed => returnUrl.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("Invalid or missing return URL.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized("User authentication failed.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.GenerateToken(user, roles);
        var expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

        var code = Guid.NewGuid().ToString("N");
        _memoryCache.Set($"sso:code:{code}", new AuthResponse
        {
            Authenticated = true,
            Email = user.Email,
            IsAdmin = roles.Contains(ApplicationRoles.Admin),
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes)
        }, TimeSpan.FromMinutes(5));

        var redirectUrl = $"{returnUrl.TrimEnd('/')}/sso-callback?code={code}";
        return Redirect(redirectUrl);
    }

    [HttpPost("sso/exchange")]
    public IActionResult ExchangeSsoCode([FromBody] SsoCodeExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Code is required.");
        }

        var cacheKey = $"sso:code:{request.Code}";
        if (!_memoryCache.TryGetValue<AuthResponse>(cacheKey, out var authResponse) || authResponse == null)
        {
            return Unauthorized("Invalid or expired code.");
        }

        _memoryCache.Remove(cacheKey);
        return Ok(authResponse);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("sso")]
    public async Task<ActionResult<IReadOnlyList<SsoConfigurationResponse>>> GetSsoConfigurations()
    {
        var configurations = await _ssoConfigurationService.GetAllAsync();
        return Ok(configurations);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("sso/status")]
    public ActionResult<SsoEnvironmentStatusResponse> GetSsoStatus()
    {
        return Ok(new SsoEnvironmentStatusResponse
        {
            IsOverriddenByEnvironment = _ssoConfigurationService.IsOverriddenByEnvironment
        });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("auth")]
    [HttpPut("sso")]
    public async Task<ActionResult<SsoConfigurationResponse>> UpsertSsoConfiguration([FromBody] UpsertSsoConfigurationRequest request)
    {
        if (_ssoConfigurationService.IsOverriddenByEnvironment)
        {
            return Conflict("SSO configuration is managed via environment variable and cannot be modified through the UI.");
        }

        var configuration = await _ssoConfigurationService.UpsertAsync(request);
        return Ok(configuration);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpDelete("sso/{schemeName}")]
    public async Task<IActionResult> DeleteSsoConfiguration(string schemeName)
    {
        if (_ssoConfigurationService.IsOverriddenByEnvironment)
        {
            return Conflict("SSO configuration is managed via environment variable and cannot be modified through the UI.");
        }

        await _ssoConfigurationService.DeleteAsync(schemeName);
        return NoContent();
    }
}
