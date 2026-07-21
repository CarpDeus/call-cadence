using CallCadence.API.Auth;
using CallCadence.Infrastructure.ApiCall;
using CallCadence.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CallCadence.UnitTests.Auth;

[TestFixture]
public sealed class AdminAuthServiceTests
{
    private ServiceProvider _serviceProvider = null!;
    private CallCadenceDbContext _dbContext = null!;
    private UserManager<AdminUser> _userManager = null!;
    private RoleManager<IdentityRole> _roleManager = null!;
    private AdminAuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddHttpContextAccessor();
        services.AddSingleton<IHttpContextAccessor>(_ =>
        {
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            return accessor;
        });
        services.AddDbContext<CallCadenceDbContext>(options =>
            options.UseInMemoryDatabase($"AdminAuthServiceTests_{Guid.NewGuid()}"));
        services.AddIdentityCore<AdminUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CallCadenceDbContext>()
            .AddSignInManager<SignInManager<AdminUser>>()
            .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<CallCadenceDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<AdminUser>>();
        _roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        _service = ActivatorUtilities.CreateInstance<AdminAuthService>(_serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _roleManager.Dispose();
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task CreateUserAsync_WhenRequestIsValid_CreatesNonAdminUser()
    {
        var createdUser = await _service.CreateUserAsync(new CreateUserRequest
        {
            Email = "user@example.com",
            Password = "Password123!",
            IsAdmin = false
        });

        createdUser.Email.Should().Be("user@example.com");
        createdUser.IsAdmin.Should().BeFalse();
        createdUser.IsDeactivated.Should().BeFalse();
    }

    [Test]
    public async Task CreateUserAsync_WhenAdminRequested_AssignsAdminRole()
    {
        var createdUser = await _service.CreateUserAsync(new CreateUserRequest
        {
            Email = "admin@example.com",
            Password = "Password123!",
            IsAdmin = true
        });

        createdUser.IsAdmin.Should().BeTrue();
        var roleExists = await _roleManager.RoleExistsAsync(ApplicationRoles.Admin);
        roleExists.Should().BeTrue();
    }

    [Test]
    public async Task UpdateUserAsync_WhenRemovingLastActiveAdmin_ThrowsInvalidOperationException()
    {
        var admin = await CreateUserAsync("admin@example.com", isAdmin: true);

        var act = async () => await _service.UpdateUserAsync(admin.Id, new UpdateUserRequest
        {
            IsAdmin = false
        }, currentUserId: "another-admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("At least one active admin account is required.");
    }

    [Test]
    public async Task DeactivateUserAsync_WhenUserIsRegularUser_LocksTheAccount()
    {
        var user = await CreateUserAsync("user@example.com", isAdmin: false);

        await _service.DeactivateUserAsync(user.Id, currentUserId: "admin-id");

        var reloadedUser = await _userManager.FindByIdAsync(user.Id);
        reloadedUser.Should().NotBeNull();
        reloadedUser!.LockoutEnd.Should().NotBeNull();
        reloadedUser.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
    }

    private async Task<AdminUser> CreateUserAsync(string email, bool isAdmin)
    {
        var user = new AdminUser
        {
            UserName = email,
            Email = email,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, "Password123!");
        createResult.Succeeded.Should().BeTrue();

        if (isAdmin)
        {
            if (!await _roleManager.RoleExistsAsync(ApplicationRoles.Admin))
            {
                await _roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Admin));
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            addToRoleResult.Succeeded.Should().BeTrue();
        }

        return user;
    }
}
