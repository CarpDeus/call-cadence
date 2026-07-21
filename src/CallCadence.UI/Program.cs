using CallCadence.UI.Components;
using CallCadence.UI.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<UserSessionState>();
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddScoped<HttpClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    // Resolved in priority order: environment variable (Api__BaseUrl) → appsettings.json (Api:BaseUrl).
    // Missing value is a fatal configuration error.
    var baseUrl = configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Api:BaseUrl configuration is required. " +
            "Set the Api__BaseUrl environment variable or add Api:BaseUrl to appsettings.json.");

    var bearerHandler = serviceProvider.GetRequiredService<BearerTokenHandler>();
    bearerHandler.InnerHandler = new HttpClientHandler();

    var client = new HttpClient(bearerHandler)
    {
        BaseAddress = new Uri(baseUrl)
    };

    return client;
});
builder.Services.AddScoped<CallCadenceApiClient>();

var app = builder.Build();

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

app.Run();

