using CallCadence.UI.Components;
using CallCadence.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from configuration. Sinks (console/file) and levels are defined in
// appsettings.json under the "Serilog" section, so logging is fully optional and
// configurable per environment. If no Serilog config is present, this is a no-op and the
// default logging providers remain in effect.
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

// Add MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<UserSessionState>();
builder.Services.AddScoped<BearerTokenHandler>();

// Standard Blazor authentication/authorization services.
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<TokenAuthenticationStateProvider>());

// Resolve the API base URL once. Priority: environment variable (Api__BaseUrl) → appsettings.json (Api:BaseUrl).
// Missing value is a fatal configuration error.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Api:BaseUrl configuration is required. " +
        "Set the Api__BaseUrl environment variable or add Api:BaseUrl to appsettings.json.");

// HttpClient built inside the Blazor circuit scope so BearerTokenHandler shares the SAME
// scoped UserSessionState that TokenAuthenticationStateProvider updates on login.
// IHttpClientFactory caches its handler chain in a separate scope, giving the handler a
// different (empty) UserSessionState — which caused 401s right after a successful login.
builder.Services.AddScoped<HttpClient>(serviceProvider =>
{
    var bearerHandler = serviceProvider.GetRequiredService<BearerTokenHandler>();
    bearerHandler.InnerHandler = new HttpClientHandler();

    return new HttpClient(bearerHandler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});
builder.Services.AddScoped<CallCadenceApiClient>();
var app = builder.Build();

// Emit a concise, structured log line per HTTP request (honors the configured Serilog levels).
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Non-fatal API readiness probe. Poll the API's public status endpoint a few times so
// startup logs indicate whether the API is reachable, but never block or fail startup —
// Home.razor already polls at runtime and the app must come up even if the API is down.
await ProbeApiReadinessAsync(app);

app.Run();

static async Task ProbeApiReadinessAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var baseUrl = app.Configuration["Api:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        logger.LogWarning("Api:BaseUrl is not configured; skipping API readiness probe.");
        return;
    }

    var timeoutSeconds = app.Configuration.GetValue("Api:ReadinessTimeoutSeconds", 15);
    var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0, timeoutSeconds));

    using var httpClient = new HttpClient
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(3)
    };

    var attempt = 0;
    while (DateTime.UtcNow < deadline)
    {
        attempt++;
        try
        {
            using var response = await httpClient.GetAsync("api/auth/status");
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "API readiness probe succeeded on attempt {Attempt}: {BaseUrl} is reachable.",
                    attempt, baseUrl);
                return;
            }

            logger.LogInformation(
                "API readiness probe attempt {Attempt} returned {StatusCode}; retrying.",
                attempt, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                "API readiness probe attempt {Attempt} failed ({Reason}); the app will start anyway.",
                attempt, ex.Message);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        catch (TaskCanceledException)
        {
            break;
        }
    }

    logger.LogWarning(
        "API at {BaseUrl} was not reachable within {TimeoutSeconds}s. Starting the UI anyway; " +
        "pages that depend on the API will retry at runtime.",
        baseUrl, timeoutSeconds);
}

