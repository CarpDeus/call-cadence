using System.Net;
using System.Net.Http.Json;
using CallCadence.API.Auth;
using CallCadence.Infrastructure.ApiCall;
using CallCadence.IntegrationTests.TestAuthentication;
using CallCadence.Models.Auth;
using FluentAssertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace CallCadence.IntegrationTests.Controllers;

[TestFixture]
public sealed class AuthControllerTests
{
    private readonly List<IDisposable> _disposables = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _disposables.Clear();
    }

    [Test]
    public async Task Register_WhenNoAdminExists_CreatesAuthenticatedAdmin()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdminRequest
        {
            Email = "admin@example.com",
            Password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.Authenticated.Should().BeTrue();
        payload.IsAdmin.Should().BeTrue();
    }

    [Test]
    public async Task Register_WhenNoAdminExists_IssuesJwtToken()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdminRequest
        {
            Email = "admin@example.com",
            Password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        payload.ExpiresAtUtc.Should().NotBeNull();
        payload.ExpiresAtUtc!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Test]
    public async Task Login_WhenValidCredentials_IssuesJwtToken()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        await SeedUserAsync(factory, "user@example.com", "Password123!", isAdmin: true, isDeactivated: false);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.Authenticated.Should().BeTrue();
        payload.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetStatus_WithBearerToken_ReturnsAuthenticated()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        await SeedUserAsync(factory, "user@example.com", "Password123!", isAdmin: true, isDeactivated: false);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        login!.Token.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.Token);

        var statusResponse = await client.GetAsync("/api/auth/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<AuthStatusResponse>();
        status.Should().NotBeNull();
        status!.IsAuthenticated.Should().BeTrue();
        status.CurrentUserEmail.Should().Be("user@example.com");
        status.CurrentUserIsAdmin.Should().BeTrue();
    }

    [Test]
    public async Task ExchangeSsoCode_WithInvalidCode_ReturnsUnauthorized()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        var response = await client.PostAsJsonAsync("/api/auth/sso/exchange", new SsoCodeExchangeRequest
        {
            Code = Guid.NewGuid().ToString("N")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WhenUserIsDeactivated_ReturnsUnauthorized()
    {
        using var factory = CreateFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();
        Track(factory, client);

        await SeedUserAsync(factory, "user@example.com", "Password123!", isAdmin: false, isDeactivated: true);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UserAdministration_WhenAdmin_CanCreateListAndDeactivateUsers()
    {
        using var factory = CreateFactory(useTestAuthentication: true);
        using var client = factory.CreateClient();
        Track(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/auth/users", new CreateUserRequest
        {
            Email = "new-user@example.com",
            Password = "Password123!",
            IsAdmin = false
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserSummaryResponse>();
        createdUser.Should().NotBeNull();

        var listResponse = await client.GetAsync("/api/auth/users");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await listResponse.Content.ReadFromJsonAsync<List<UserSummaryResponse>>();
        users.Should().ContainSingle(user => user.Email == "new-user@example.com" && !user.IsAdmin && !user.IsDeactivated);

        var deactivateResponse = await client.DeleteAsync($"/api/auth/users/{createdUser!.Id}");
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reloadedUsers = await client.GetFromJsonAsync<List<UserSummaryResponse>>("/api/auth/users");
        reloadedUsers.Should().ContainSingle(user => user.Id == createdUser.Id && user.IsDeactivated);
    }

    [Test]
    public async Task UserAdministration_WhenNonAdmin_ReturnsForbidden()
    {
        using var factory = CreateFactory(useTestAuthentication: true);
        using var client = factory.CreateClient();
        Track(factory, client);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "User");

        var response = await client.GetAsync("/api/auth/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private WebApplicationFactory<Program> CreateFactory(bool useTestAuthentication)
    {
        var databaseName = $"AuthTestDatabase_{Guid.NewGuid()}";

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:SigningKey"] = "IntegrationTestsSigningKey-AtLeast32CharactersLongForHmacSha256!!",
                        ["Jwt:Issuer"] = "CallCadence.API",
                        ["Jwt:Audience"] = "CallCadence.UI",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["AllowedUiReturnUrls:0"] = "https://localhost"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    if (useTestAuthentication)
                    {
                        services.AddTestAuthentication();
                    }

                    var descriptorsToRemove = services.Where(descriptor =>
                        descriptor.ServiceType == typeof(DbContextOptions<CallCadenceDbContext>) ||
                        descriptor.ServiceType == typeof(DbContextOptions) ||
                        descriptor.ServiceType == typeof(CallCadenceDbContext)).ToList();

                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<CallCadenceDbContext>(options => options.UseInMemoryDatabase(databaseName));

                    services.RemoveAll(typeof(IGlobalConfiguration));
                    services.RemoveAll(typeof(IBackgroundJobClient));
                    services.RemoveAll(typeof(IRecurringJobManager));
                    services.RemoveAll(typeof(JobStorage));

                    services.AddHangfire(config => config.UseMemoryStorage());
                    services.AddSingleton<IRecurringJobManager>(_ =>
                    {
                        var storage = new MemoryStorage();
                        JobStorage.Current = storage;
                        return new RecurringJobManager(storage);
                    });
                });
            });
    }

    private async Task SeedUserAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password,
        bool isAdmin,
        bool isDeactivated)
    {
        using var scope = factory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<AdminUser>>();

        if (isAdmin && !await roleManager.RoleExistsAsync(ApplicationRoles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Admin));
        }

        var user = new AdminUser
        {
            UserName = email,
            Email = email,
            LockoutEnabled = true,
            LockoutEnd = isDeactivated ? DateTimeOffset.MaxValue : null
        };

        var createResult = await userManager.CreateAsync(user, password);
        createResult.Succeeded.Should().BeTrue();

        if (isAdmin)
        {
            var addToRoleResult = await userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            addToRoleResult.Succeeded.Should().BeTrue();
        }
    }

    private void Track(params IDisposable[] disposables)
    {
        _disposables.AddRange(disposables);
    }
}
