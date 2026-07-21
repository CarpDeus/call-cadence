using System.Text;
using BugLogger.Interfaces;
using BugLogger.Services;
using CallCadence.API.Auth;
using CallCadence.API.Dashboard;
using CallCadence.API.Hubs;
using CallCadence.Application.ApiCall;
using CallCadence.Application.Tags;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Tags;
using CallCadence.Infrastructure.ApiCall;
using CallCadence.Infrastructure.Tags;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Add HttpClient factory
builder.Services.AddHttpClient();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

// Configure database connection strings based on environment
// Values are resolved from environment variables first (ConnectionStrings__CallCadenceDb),
// then from appsettings.json. Missing values are treated as fatal configuration errors
// in non-Testing environments where real database connections are required.
var apiDbConnectionString = builder.Configuration.GetConnectionString("CallCadenceDb");
var hangfireDbConnectionString = builder.Configuration.GetConnectionString("CallCadenceHangfireDb");

if (!builder.Environment.IsEnvironment("Testing"))
{
    if (apiDbConnectionString is null)
        throw new InvalidOperationException(
            "ConnectionStrings:CallCadenceDb is required. " +
            "Set the ConnectionStrings__CallCadenceDb environment variable or add it to appsettings.json.");
    if (hangfireDbConnectionString is null)
        throw new InvalidOperationException(
            "ConnectionStrings:CallCadenceHangfireDb is required. " +
            "Set the ConnectionStrings__CallCadenceHangfireDb environment variable or add it to appsettings.json.");
}

// Register DbContext
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<CallCadenceDbContext>(options =>
        options.UseSqlServer(apiDbConnectionString!));
}

// Configure Data Protection to persist keys to the database
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("CallCadence")
        .PersistKeysToDbContext<CallCadenceDbContext>();
}
else
{
    // In Testing environment, use ephemeral in-memory keys
    builder.Services.AddDataProtection();
}

// Register repositories
builder.Services.AddScoped<IApiCallRepository, ApiCallRepository>();
builder.Services.AddScoped<IApiCallArchiveRepository, ApiCallArchiveRepository>();
builder.Services.AddScoped<IApiCallLogRepository, ApiCallLogRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();

// Register application services
builder.Services.AddScoped<ApiCallManagementService>();
builder.Services.AddScoped<CallApiService>();
builder.Services.AddScoped<HangfireScheduleStartupSynchronizer>();
builder.Services.AddScoped<TagService>();
builder.Services.AddSingleton<ApiCallActivityTracker>();

// ── SSO configuration ────────────────────────────────────────────────────────
// Check for the environment variable override once at startup.
// When present it completely replaces the database configuration.
var envSsoConfigs = EnvVarSsoConfigurationProvider.TryLoad();

// Determine which SSO providers to register as named OIDC schemes.
// When running in Testing mode without a database, fall back to only env-var configs.
IReadOnlyList<SsoConfiguration> ssoConfigsAtStartup = envSsoConfigs ?? [];
if (envSsoConfigs is null && !builder.Environment.IsEnvironment("Testing")
    && apiDbConnectionString is not null)
{
    // Early DB query to discover enabled providers so we can register their schemes.
    try
    {
        var tempOptions = new DbContextOptionsBuilder<CallCadenceDbContext>()
            .UseSqlServer(apiDbConnectionString)
            .Options;
        await using var tempDb = new CallCadenceDbContext(tempOptions);
        ssoConfigsAtStartup = await tempDb.SsoConfigurations
            .Where(c => c.IsEnabled)
            .ToListAsync();
    }
    catch (Exception ex)
    {
        // DB may not be available yet; migrations are applied later. Providers
        // registered here will just have placeholder values until restart.
        // Use a temporary logger factory since the DI container is not yet built.
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        loggerFactory.CreateLogger<Program>().LogWarning(ex,
            "Could not query SSO configurations at startup — no OIDC schemes will be pre-registered. " +
            "A service restart is required after the database becomes available.");
        ssoConfigsAtStartup = [];
    }
}

// Register the env-var list as a singleton so DI can inject it into services
// that need the read-only override list.
if (envSsoConfigs is not null)
    builder.Services.AddSingleton<IReadOnlyList<SsoConfiguration>>(envSsoConfigs);

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

authBuilder.AddIdentityCookies();

// Add JWT Bearer authentication for cross-domain UI access
authBuilder.AddJwtBearer(options =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey configuration is required. " +
            "Set the Jwt__SigningKey environment variable or add Jwt:SigningKey to appsettings.json.");
    }

    var issuer = builder.Configuration["Jwt:Issuer"];
    if (string.IsNullOrWhiteSpace(issuer))
    {
        throw new InvalidOperationException("Jwt:Issuer configuration is required.");
    }

    var audience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("Jwt:Audience configuration is required.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
    };

    // Allow SignalR to read bearer token from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/generic"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Register one named OIDC scheme per enabled provider found at startup.
// DynamicOidcConfigureOptions fills in real values at first use.
foreach (var ssoConfig in ssoConfigsAtStartup)
{
    var schemeName = ssoConfig.SchemeName;
    authBuilder.AddOpenIdConnect(schemeName, options =>
    {
        // Placeholder values; DynamicOidcConfigureOptions overrides these.
        options.ClientId = "not-configured";
        options.Authority = "https://not-configured";
        options.CallbackPath = ssoConfig.CallbackPath ?? $"/signin-{schemeName}";
    });
}

// If no providers were found at startup, register one placeholder scheme so
// ASP.NET Core's OIDC infrastructure is still wired up.
if (!ssoConfigsAtStartup.Any())
{
    authBuilder.AddOpenIdConnect(options =>
    {
        options.ClientId = "not-configured";
        options.Authority = "https://not-configured";
        options.CallbackPath = "/signin-oidc";
    });
}

builder.Services.AddSingleton<IConfigureNamedOptions<OpenIdConnectOptions>>(sp =>
    new DynamicOidcConfigureOptions(
        sp.GetRequiredService<IServiceScopeFactory>(),
        envSsoConfigs));

builder.Services.AddAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(ApplicationRoles.Admin);
    });
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    var timeoutMinutes = builder.Configuration.GetValue<int>("Session:TimeoutMinutes", 30);
    options.ExpireTimeSpan = TimeSpan.FromMinutes(timeoutMinutes);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
builder.Services.AddIdentityCore<AdminUser>(options =>
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
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Register SsoConfigurationService. When env var is active, dbContext may be null
// in the scoped service – the service handles this by checking the injected env list.
if (envSsoConfigs is not null)
{
    builder.Services.AddScoped<ISsoConfigurationService>(sp =>
        new SsoConfigurationService(
            sp.GetService<CallCadenceDbContext>(),
            envSsoConfigs));
}
else
{
    builder.Services.AddScoped<ISsoConfigurationService>(sp =>
        new SsoConfigurationService(sp.GetService<CallCadenceDbContext>()));
}

// ── Sentry ────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SentrySdkInitializer>();
builder.Services.AddSingleton<ISentryService, SentryService>();

// Configure Hangfire
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireDbConnectionString!));

    builder.Services.AddHangfireServer();
}

var app = builder.Build();

// Apply EF Core migrations for relational providers on startup
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<CallCadenceDbContext>();

    if (dbContext.Database.IsRelational())
    {
        try
        {
            logger.LogInformation("Checking for pending database migrations...");

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            var pendingMigrationsList = pendingMigrations.ToList();

            if (pendingMigrationsList.Count > 0)
            {
                logger.LogInformation("Found {MigrationCount} pending migration(s): {Migrations}",
                    pendingMigrationsList.Count,
                    string.Join(", ", pendingMigrationsList));

                logger.LogInformation("Applying database migrations...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                logger.LogInformation("Database is up to date. No pending migrations found");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations. Application startup aborted");
            throw;
        }
    }
    else
    {
        logger.LogInformation("Non-relational database provider detected. Skipping migrations");
    }
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var startupSynchronizer = scope.ServiceProvider.GetRequiredService<HangfireScheduleStartupSynchronizer>();
    await startupSynchronizer.SynchronizeAsync();
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (!app.Environment.IsEnvironment("Testing"))
{
    _ = app.Services.GetRequiredService<SentrySdkInitializer>();
}

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.MapControllers();
app.MapHub<GenericHub>("/hubs/generic");

app.Run();

// Make Program class accessible for testing
public partial class Program { }
