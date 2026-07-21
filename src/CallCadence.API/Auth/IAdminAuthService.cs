using System.Security.Claims;
using CallCadence.Models.Auth;

namespace CallCadence.API.Auth;

public interface IAdminAuthService
{
    Task<bool> HasAdminAsync();
    Task<AuthStatusResponse> GetStatusAsync(ClaimsPrincipal principal);
    Task<AuthResponse> RegisterAdminAsync(RegisterAdminRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync();
    Task<UserSummaryResponse> CreateUserAsync(CreateUserRequest request);
    Task<UserSummaryResponse> UpdateUserAsync(string userId, UpdateUserRequest request, string? currentUserId);
    Task SetPasswordAsync(string userId, SetUserPasswordRequest request);
    Task DeactivateUserAsync(string userId, string? currentUserId);
    Task LogoutAsync();
}
