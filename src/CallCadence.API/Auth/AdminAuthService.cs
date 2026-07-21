using System.Security.Claims;
using CallCadence.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CallCadence.API.Auth;

public sealed class AdminAuthService : IAdminAuthService
{
    private const string HasAdminCacheKey = "auth:has-admin";
    private readonly UserManager<AdminUser> _userManager;
    private readonly SignInManager<AdminUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IMemoryCache _memoryCache;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public AdminAuthService(
        UserManager<AdminUser> userManager,
        SignInManager<AdminUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IMemoryCache memoryCache,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _memoryCache = memoryCache;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<bool> HasAdminAsync()
    {
        if (_memoryCache.TryGetValue(HasAdminCacheKey, out bool hasAdmin))
        {
            return hasAdmin;
        }

        hasAdmin = await HasAnyAdminAsync();
        _memoryCache.Set(HasAdminCacheKey, hasAdmin, TimeSpan.FromMinutes(1));
        return hasAdmin;
    }

    public async Task<AuthStatusResponse> GetStatusAsync(ClaimsPrincipal principal)
    {
        var hasAdmin = await HasAdminAsync();
        var isAuthenticated = principal.Identity?.IsAuthenticated == true;

        return new AuthStatusResponse
        {
            HasAdmin = hasAdmin,
            IsAuthenticated = isAuthenticated,
            CurrentUserEmail = isAuthenticated ? principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name : null,
            CurrentUserIsAdmin = isAuthenticated && principal.IsInRole(ApplicationRoles.Admin)
        };
    }

    public async Task<AuthResponse> RegisterAdminAsync(RegisterAdminRequest request)
    {
        if (!IsValidEmail(request.Email) || !IsValidPassword(request.Password))
        {
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Email and password are required; password must be 12+ chars with upper, lower, digit, and symbol."
            };
        }

        if (await HasAnyAdminAsync())
        {
            _memoryCache.Set(HasAdminCacheKey, true, TimeSpan.FromMinutes(1));
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "An admin account already exists."
            };
        }

        await EnsureAdminRoleExistsAsync();

        var email = NormalizeEmail(request.Email);
        var user = new AdminUser
        {
            UserName = email,
            Email = email,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = GetErrorDescription(createResult, "Unable to create admin account.")
            };
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
        if (!addToRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = GetErrorDescription(addToRoleResult, "Unable to grant admin access.")
            };
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        _memoryCache.Set(HasAdminCacheKey, true, TimeSpan.FromMinutes(1));

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.GenerateToken(user, roles);
        var expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

        return new AuthResponse
        {
            Authenticated = true,
            Email = user.Email,
            IsAdmin = true,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (!IsValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Email and password are required."
            };
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || IsDeactivated(user))
        {
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Invalid login."
            };
        }

        var signInResult = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            return new AuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Invalid login."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.GenerateToken(user, roles);
        var expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

        return new AuthResponse
        {
            Authenticated = true,
            Email = user.Email,
            IsAdmin = await _userManager.IsInRoleAsync(user, ApplicationRoles.Admin),
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    public async Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();

        var result = new List<UserSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            result.Add(await MapUserAsync(user));
        }

        return result;
    }

    public async Task<UserSummaryResponse> CreateUserAsync(CreateUserRequest request)
    {
        if (!IsValidEmail(request.Email))
        {
            throw new ArgumentException("A valid email address is required.");
        }

        if (!IsValidPassword(request.Password))
        {
            throw new ArgumentException("Password must be 12+ chars with upper, lower, digit, and symbol.");
        }

        if (request.IsAdmin)
        {
            await EnsureAdminRoleExistsAsync();
        }

        var email = NormalizeEmail(request.Email);
        var user = new AdminUser
        {
            UserName = email,
            Email = email,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new ArgumentException(GetErrorDescription(createResult, "Unable to create user."));
        }

        if (!request.IsAdmin)
        {
            return await MapUserAsync(user);
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
        if (!addToRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new ArgumentException(GetErrorDescription(addToRoleResult, "Unable to grant admin access."));
        }

        _memoryCache.Remove(HasAdminCacheKey);
        return await MapUserAsync(user);
    }

    public async Task<UserSummaryResponse> UpdateUserAsync(string userId, UpdateUserRequest request, string? currentUserId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        await EnsureAdminRoleExistsAsync();

        var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, ApplicationRoles.Admin);
        if (isCurrentlyAdmin == request.IsAdmin)
        {
            return await MapUserAsync(user);
        }

        if (!request.IsAdmin)
        {
            await EnsureAdminCanBeRemovedAsync(user, currentUserId);

            var removeResult = await _userManager.RemoveFromRoleAsync(user, ApplicationRoles.Admin);
            if (!removeResult.Succeeded)
            {
                throw new ArgumentException(GetErrorDescription(removeResult, "Unable to remove admin access."));
            }
        }
        else
        {
            var addResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            if (!addResult.Succeeded)
            {
                throw new ArgumentException(GetErrorDescription(addResult, "Unable to grant admin access."));
            }
        }

        await _userManager.UpdateSecurityStampAsync(user);
        _memoryCache.Remove(HasAdminCacheKey);
        return await MapUserAsync(user);
    }

    public async Task SetPasswordAsync(string userId, SetUserPasswordRequest request)
    {
        if (!IsValidPassword(request.Password))
        {
            throw new ArgumentException("Password must be 12+ chars with upper, lower, digit, and symbol.");
        }

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, request.Password);
        if (!resetResult.Succeeded)
        {
            throw new ArgumentException(GetErrorDescription(resetResult, "Unable to reset password."));
        }
    }

    public async Task DeactivateUserAsync(string userId, string? currentUserId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        if (IsDeactivated(user))
        {
            return;
        }

        await EnsureAdminCanBeRemovedAsync(user, currentUserId);

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new ArgumentException(GetErrorDescription(updateResult, "Unable to deactivate user."));
        }

        await _userManager.UpdateSecurityStampAsync(user);
    }

    private async Task<bool> HasAnyAdminAsync()
    {
        if (!await _roleManager.RoleExistsAsync(ApplicationRoles.Admin))
        {
            return false;
        }

        var admins = await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        return admins.Any(user => !IsDeactivated(user));
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (await _roleManager.RoleExistsAsync(ApplicationRoles.Admin))
        {
            return;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Admin));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(GetErrorDescription(result, "Unable to initialize admin role."));
        }
    }

    private async Task EnsureAdminCanBeRemovedAsync(AdminUser user, string? currentUserId)
    {
        if (!await _userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
        {
            return;
        }

        if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("You cannot remove your own admin access or deactivate your own account.");
        }

        var adminUsers = await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        var activeAdminCount = adminUsers.Count(adminUser => !IsDeactivated(adminUser));
        if (activeAdminCount <= 1)
        {
            throw new InvalidOperationException("At least one active admin account is required.");
        }
    }

    private async Task<UserSummaryResponse> MapUserAsync(AdminUser user)
    {
        return new UserSummaryResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            IsAdmin = await _userManager.IsInRoleAsync(user, ApplicationRoles.Admin),
            IsDeactivated = IsDeactivated(user)
        };
    }

    private static string GetErrorDescription(IdentityResult result, string fallbackMessage)
    {
        return result.Errors.FirstOrDefault()?.Description ?? fallbackMessage;
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email);
    }

    private static bool IsValidPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password)
            && password.Length >= 12
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(ch => !char.IsLetterOrDigit(ch));
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static bool IsDeactivated(AdminUser user)
    {
        return user.LockoutEnabled
            && user.LockoutEnd.HasValue
            && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }
}
